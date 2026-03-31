#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using Morphyn.Parser;
using Morphyn.Runtime;
using Morphyn.Unity;

namespace Morphyn.Unity.Editor
{
    [CustomEditor(typeof(MorphynEntity))]
    public class MorphynEntityInspector : UnityEditor.Editor
    {
        private MorphynEntity _target;
        private bool _showFields = true;
        private bool _showEvents = true;
        private Entity _entity;
        private GUIStyle _headerStyle;
        private GUIStyle _rowStyle;
        private Dictionary<string, MorphynValue> _editedFields = new();

        private void OnEnable()
        {
            _target = (MorphynEntity)target;
            EditorApplication.update += OnEditorUpdate;
            RefreshEntity();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_target == null) return;

            if (Application.isPlaying)
            {
                var controller = MorphynController.Instance;
                if (controller?.Context != null && _target.EntityName != null)
                {
                    controller.Context.Entities.TryGetValue(_target.EntityName, out _entity);
                }
            }
            else
            {
                var parsed = ParseMorphFileInEditor();
                if (parsed != null)
                {
                    _entity = parsed;
                    ApplyEditedFields();
                }
            }

            Repaint();
        }

        private void RefreshEntity()
        {
            if (Application.isPlaying)
            {
                var controller = MorphynController.Instance;
                if (controller?.Context != null && _target.EntityName != null)
                {
                    controller.Context.Entities.TryGetValue(_target.EntityName, out _entity);
                }
            }
            else
            {
                var parsed = ParseMorphFileInEditor();
                if (parsed != null)
                {
                    _entity = parsed;
                    ApplyEditedFields();
                }
            }
        }

        private void ApplyEditedFields()
        {
            foreach (var kvp in _editedFields)
            {
                if (_entity.Fields.ContainsKey(kvp.Key))
                {
                    _entity.Fields[kvp.Key] = kvp.Value;
                }
            }
        }

        private Entity ParseMorphFileInEditor()
        {
            if (_target.MorphScript == null) return null;

            try
            {
                MorphynParser.OnError = msg => Debug.LogWarning($"[Morphyn Parser] {msg}");

                string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(_target.MorphScript);
                string combinedCode = _target.MorphScript.text;

                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string fullPath = System.IO.Path.GetFullPath(scriptPath);
                    combinedCode = ResolveImportsForEditor(fullPath, new System.Collections.Generic.HashSet<string>());
                }

                var data = MorphynParser.ParseFile(combinedCode);

                if (data == null || data.Entities.Count == 0) return null;

                if (_target.MorphScript != null)
                {
                    var scriptName = _target.MorphScript.name;
                    if (data.Entities.TryGetValue(scriptName, out var entity))
                        return entity;

                    if (data.Entities.Count == 1)
                        return data.Entities.Values.First();
                }

                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Morphyn Inspector] Parse failed: {ex.Message}");
                return null;
            }
        }

        private string ResolveImportsForEditor(string absolutePath, System.Collections.Generic.HashSet<string> visited)
        {
            if (visited.Contains(absolutePath)) return "";
            visited.Add(absolutePath);

            if (!System.IO.File.Exists(absolutePath)) return "";

            string content = System.IO.File.ReadAllText(absolutePath);
            string[] lines = content.Split('\n');
            var finalContent = new System.Collections.Generic.List<string>();
            string currentDir = System.IO.Path.GetDirectoryName(absolutePath);

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("import ") && trimmed.Contains("\""))
                {
                    int firstQuote = trimmed.IndexOf('"');
                    int lastQuote = trimmed.LastIndexOf('"');
                    if (firstQuote != -1 && lastQuote > firstQuote)
                    {
                        string relativePath = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        string fullSubPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(currentDir, relativePath));
                        if (System.IO.File.Exists(fullSubPath))
                            finalContent.Add(ResolveImportsForEditor(fullSubPath, visited));
                        continue;
                    }
                }
                finalContent.Add(line);
            }

            return string.Join("\n", finalContent);
        }

        private void InitializeStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(4, 4, 8, 8)
                };
            }

            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle()
                {
                    padding = new RectOffset(4, 4, 2, 2),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Morphyn Entity", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawScriptProperties();

            EditorGUILayout.Space(8);

            if (_entity != null)
            {
                if (Application.isPlaying)
                {
                    EditorGUILayout.HelpBox($"Entity registered: {_target.EntityName}", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Entity parsed: {_entity.Name} ({_entity.Fields.Count} fields, {_entity.Events.Count} events)", MessageType.Info);
                }

                EditorGUILayout.Space(4);
                DrawFieldsSection();
                EditorGUILayout.Space(4);
                DrawEventsSection();
            }
            else
            {
                if (_target.MorphScript == null)
                {
                    EditorGUILayout.HelpBox("No .morph script assigned. Drag a TextAsset into the Script field.", MessageType.Warning);
                }
                else if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox($"Failed to parse '{_target.MorphScript.name}'. Check:\n• Entity name matches\n• .morph file syntax is valid\n• Check Console for errors", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("Entity not found in MorphynController. Check registration in Play Mode console.", MessageType.Error);
                }
            }
        }

        private void DrawScriptProperties()
        {
            EditorGUI.indentLevel++;

            var scriptProp = serializedObject.FindProperty("morphScript");
            EditorGUILayout.PropertyField(scriptProp, new GUIContent("Script"));

            var customNameProp = serializedObject.FindProperty("customEntityName");
            EditorGUILayout.PropertyField(customNameProp, new GUIContent("Custom Entity Name"));

            var autoSaveProp = serializedObject.FindProperty("autoSaveOnDestroy");
            EditorGUILayout.PropertyField(autoSaveProp, new GUIContent("Auto Save On Destroy"));

            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFieldsSection()
        {
            if (_entity == null || _entity.Fields.Count == 0)
                return;

            _showFields = EditorGUILayout.Foldout(_showFields, $"Fields ({_entity.Fields.Count})", _headerStyle);

            if (!_showFields)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            try
            {
                var fields = _entity.Fields.OrderBy(f => f.Key).ToList();
                foreach (var kvp in fields)
                {
                    DrawFieldRow(kvp.Key, kvp.Value);
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFieldRow(string fieldName, MorphynValue fieldValue)
        {
            EditorGUILayout.BeginHorizontal(_rowStyle);

            object currentValue = fieldValue.ToObject();
            object newValue = currentValue;

            try
            {
                EditorGUILayout.LabelField(fieldName, GUILayout.Width(120));

                if (currentValue is bool boolVal)
                {
                    newValue = EditorGUILayout.Toggle(boolVal, GUILayout.ExpandWidth(true));
                }
                else if (currentValue is double doubleVal)
                {
                    newValue = EditorGUILayout.DoubleField(doubleVal, GUILayout.ExpandWidth(true));
                }
                else if (currentValue is float floatVal)
                {
                    newValue = EditorGUILayout.FloatField(floatVal, GUILayout.ExpandWidth(true));
                }
                else if (currentValue is string stringVal)
                {
                    newValue = EditorGUILayout.TextField(stringVal, GUILayout.ExpandWidth(true));
                }
                else if (currentValue is MorphynPool pool)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField($"pool[{pool.Values.Count}]", GUILayout.ExpandWidth(true));
                    EditorGUI.EndDisabledGroup();
                }
                else if (currentValue == null)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField("null", GUILayout.ExpandWidth(true));
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField(currentValue.ToString(), GUILayout.ExpandWidth(true));
                    EditorGUI.EndDisabledGroup();
                }

                if (!Equals(newValue, currentValue))
                {
                    if (Application.isPlaying)
                    {
                        _target.SetField(fieldName, MorphynValue.FromObject(newValue));
                    }
                    else
                    {
                        var newMorphValue = MorphynValue.FromObject(newValue);
                        _entity.Fields[fieldName] = newMorphValue;
                        _editedFields[fieldName] = newMorphValue;
                        PatchFieldInMorphFile(fieldName, newMorphValue);
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void PatchFieldInMorphFile(string fieldName, MorphynValue newValue)
        {
            if (_target.MorphScript == null) return;

            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(_target.MorphScript);
            if (string.IsNullOrEmpty(assetPath)) return;

            string fullPath = System.IO.Path.GetFullPath(assetPath);
            if (!System.IO.File.Exists(fullPath)) return;

            string[] lines = System.IO.File.ReadAllLines(fullPath);
            bool patched = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("has ") && trimmed.Contains(fieldName + ":"))
                {
                    string indent = lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length);
                    string formattedValue = FormatValueForFile(newValue);
                    lines[i] = $"{indent}has {fieldName}: {formattedValue}";
                    patched = true;
                    break;
                }
            }

            if (!patched) return;

            System.IO.File.WriteAllLines(fullPath, lines);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);
        }

        private string FormatValueForFile(MorphynValue value)
        {
            object raw = value.ToObject();
            return raw switch
            {
                null => "null",
                bool b => b.ToString().ToLower(),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string s => $"\"{s}\"",
                _ => raw.ToString() ?? "null"
            };
        }

        private void DrawEventsSection()
        {
            if (_entity == null || _entity.Events.Count == 0)
                return;

            _showEvents = EditorGUILayout.Foldout(_showEvents, $"Events ({_entity.Events.Count})", _headerStyle);

            if (!_showEvents)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            try
            {
                var events = _entity.Events.OrderBy(e => e.Name).ToList();
                foreach (var evt in events)
                {
                    EditorGUILayout.BeginHorizontal(_rowStyle);
                    try
                    {
                        EditorGUILayout.LabelField(evt.Name, GUILayout.Width(120));
                        EditorGUILayout.LabelField($"({evt.Parameters.Count} params)", GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("Emit", GUILayout.Width(50)))
                        {
                            _target.Emit(evt.Name);
                        }
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif