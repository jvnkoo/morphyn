using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Unity
{
    /// <summary>
    /// Bind a .morph script to a GameObject.
    /// Automatically parses and registers the entity in MorphynController.
    /// Displays entity fields in the Inspector during Edit and Play modes.
    /// </summary>
    [DisallowMultipleComponent]
    public class MorphynEntity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The .morph script file to load")]
        private TextAsset morphScript;

        [SerializeField]
        [Tooltip("Entity name (defaults to script name if empty)")]
        private string customEntityName = "";

        [SerializeField]
        [Tooltip("Auto-save entity state when destroyed")]
        private bool autoSaveOnDestroy = false;

        private string _registeredEntityName;
        private Entity _parsedEntity;
        private bool _isInitialized = false;

        public string EntityName => _registeredEntityName;
        public Entity ParsedEntity => _parsedEntity;
        public TextAsset MorphScript => morphScript;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                TryParseMorphFile();
            }
        }

        private void Awake()
        {
            if (morphScript == null)
            {
                Debug.LogError($"[Morphyn] MorphynEntity on '{gameObject.name}' has no script assigned!", gameObject);
                return;
            }

            _registeredEntityName = string.IsNullOrWhiteSpace(customEntityName) 
                ? morphScript.name 
                : customEntityName;
        }

        private void Start()
        {
            if (MorphynController.Instance?.Context != null)
            {
                RegisterEntity();
            }
            else
            {
                MorphynController.OnContextReady += RegisterEntity;
            }
        }

        private void TryParseMorphFile()
        {
            if (morphScript == null) 
            {
                _parsedEntity = null;
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(morphScript.text))
                {
                    Debug.LogWarning($"[Morphyn] Script '{morphScript.name}' is empty!");
                    _parsedEntity = null;
                    return;
                }

                MorphynParser.OnError = msg => Debug.LogWarning($"[Morphyn Parser] {msg}");

#if UNITY_EDITOR
                string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(morphScript);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string fullPath = Path.GetFullPath(scriptPath);
                    string combinedCode = ResolveImports(fullPath, new HashSet<string>());
                    var data = MorphynParser.ParseFile(combinedCode);

                    if (data == null || data.Entities.Count == 0)
                    {
                        Debug.LogWarning($"[Morphyn] Script '{morphScript.name}' contains no entities! Check syntax.");
                        _parsedEntity = null;
                        return;
                    }

                    string targetName = string.IsNullOrWhiteSpace(customEntityName) 
                        ? morphScript.name 
                        : customEntityName;

                    if (data.Entities.TryGetValue(targetName, out var entity))
                    {
                        _parsedEntity = entity;
                        Debug.Log($"[Morphyn] Parsed entity '{targetName}' from '{morphScript.name}'");
                    }
                    else if (data.Entities.Count == 1)
                    {
                        _parsedEntity = data.Entities.Values.First();
                        Debug.Log($"[Morphyn] Parsed entity '{_parsedEntity.Name}' from '{morphScript.name}'");
                    }
                    else
                    {
                        Debug.LogWarning($"[Morphyn] Script '{morphScript.name}' has {data.Entities.Count} entities: {string.Join(", ", data.Entities.Keys)}. Specify 'Custom Entity Name'.");
                        _parsedEntity = null;
                    }
                }
#else
                var data = MorphynParser.ParseFile(morphScript.text);

                if (data == null || data.Entities.Count == 0)
                {
                    Debug.LogWarning($"[Morphyn] Script '{morphScript.name}' contains no entities! Check syntax.");
                    _parsedEntity = null;
                    return;
                }

                string targetName = string.IsNullOrWhiteSpace(customEntityName) 
                    ? morphScript.name 
                    : customEntityName;

                if (data.Entities.TryGetValue(targetName, out var entity))
                {
                    _parsedEntity = entity;
                }
                else if (data.Entities.Count == 1)
                {
                    _parsedEntity = data.Entities.Values.First();
                }
                else
                {
                    _parsedEntity = null;
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Morphyn] Failed to parse script: {ex.Message}");
                _parsedEntity = null;
            }
        }

        /// <summary>
        /// Parse the .morph file and register entity in MorphynController.
        /// This is called automatically after a delay to ensure MorphynController is ready.
        /// </summary>
        private void RegisterEntity()
        {
            if (morphScript == null) return;

            MorphynController controller = MorphynController.Instance;
            if (controller == null || controller.Context == null) return;

            try
            {
#if UNITY_EDITOR
                string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(morphScript);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string fullPath = Path.GetFullPath(scriptPath);
                    string combinedCode = ResolveImports(fullPath, new HashSet<string>());
                    var data = MorphynParser.ParseFile(combinedCode);
                    
                    if (data == null || data.Entities.Count == 0) return;

                    // Register all parsed entities — imports like 'math' must land in the
                    // shared context so EmitSync calls across entity boundaries can find them.
                    foreach (var kvp in data.Entities)
                    {
                        if (controller.Context.Entities.ContainsKey(kvp.Key)) continue;

                        kvp.Value.BuildCache();
                        controller.Context.Entities[kvp.Key] = kvp.Value;

                        if (kvp.Value.Events.Any(e => e.Name == "init"))
                            MorphynRuntime.Send(kvp.Value, "init");
                    }

                    Entity entityToRegister = data.Entities.ContainsKey(_registeredEntityName)
                        ? data.Entities[_registeredEntityName]
                        : data.Entities.Values.First();

                    // Re-assign in case it was already inserted above
                    entityToRegister.BuildCache();
                    controller.Context.Entities[_registeredEntityName] = entityToRegister;
                    _parsedEntity = entityToRegister;

                    MorphynRuntime.RunFullCycle(controller.Context);

                    Debug.Log($"[Morphyn] Entity '{_registeredEntityName}' registered from '{gameObject.name}'");
                    _isInitialized = true;
                }
#else
                MorphynParser.OnError = msg => Debug.LogWarning($"[Morphyn Parser] {msg}");
                var data = MorphynParser.ParseFile(morphScript.text);

                if (data == null || data.Entities.Count == 0) return;

                // Register all parsed entities — imports like 'math' must land in the
                // shared context so EmitSync calls across entity boundaries can find them.
                foreach (var kvp in data.Entities)
                {
                    if (controller.Context.Entities.ContainsKey(kvp.Key)) continue;

                    kvp.Value.BuildCache();
                    controller.Context.Entities[kvp.Key] = kvp.Value;

                    if (kvp.Value.Events.Any(e => e.Name == "init"))
                        MorphynRuntime.Send(kvp.Value, "init");
                }

                Entity entityToRegister = data.Entities.ContainsKey(_registeredEntityName)
                    ? data.Entities[_registeredEntityName]
                    : data.Entities.Values.First();

                entityToRegister.BuildCache();
                controller.Context.Entities[_registeredEntityName] = entityToRegister;
                _parsedEntity = entityToRegister;

                MorphynRuntime.RunFullCycle(controller.Context);

                Debug.Log($"[Morphyn] Entity '{_registeredEntityName}' registered from '{gameObject.name}'");
                _isInitialized = true;
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Morphyn] Failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string ResolveImports(string absolutePath, HashSet<string> visited)
        {
            if (visited.Contains(absolutePath)) return "";
            visited.Add(absolutePath);

            if (!File.Exists(absolutePath)) return "";

            string content = File.ReadAllText(absolutePath);
            string[] lines = content.Split('\n');
            var finalContent = new List<string>();
            string currentDir = Path.GetDirectoryName(absolutePath);

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
                        string fullSubPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
                        if (File.Exists(fullSubPath))
                        {
                            finalContent.Add(ResolveImports(fullSubPath, visited));
                        }
                        else
                        {
                            // Fallback: try standard library via AssetDatabase
                            string? stdlib = MorphynController.TryLoadStdlib(relativePath);
                            if (stdlib != null)
                                finalContent.Add(stdlib);
                            else
                                Debug.LogWarning($"[Morphyn] Import not found: {fullSubPath}");
                        }
                        continue;
                    }
                }
                finalContent.Add(line);
            }

            return string.Join("\n", finalContent);
        }

        private void OnDestroy()
        {
            MorphynController.OnContextReady -= RegisterEntity;

            if (!_isInitialized) return;

            MorphynController controller = MorphynController.Instance;
            if (controller?.Context == null) return;

            if (autoSaveOnDestroy && controller.Context.Entities.TryGetValue(_registeredEntityName, out var entity))
            {
                string savePath = System.IO.Path.Combine(Application.persistentDataPath, "MorphynData");
                if (!System.IO.Directory.Exists(savePath))
                    System.IO.Directory.CreateDirectory(savePath);

                string filePath = System.IO.Path.Combine(savePath, $"{_registeredEntityName}.morph");
                MorphynSerializer.SaveEntity(entity, filePath);
                Debug.Log($"[Morphyn] Auto-saved entity '{_registeredEntityName}'");
            }

            if (controller.Context.Entities.Remove(_registeredEntityName))
            {
                Debug.Log($"[Morphyn] Unregistered entity '{_registeredEntityName}'");
            }
        }

        /// <summary>
        /// Emit an event on this entity
        /// </summary>
        public void Emit(string eventName, params object[] args)
        {
            MorphynController.Instance?.Emit(_registeredEntityName, eventName, args);
        }

        /// <summary>
        /// Get a field value
        /// </summary>
        public MorphynValue GetField(string fieldName)
        {
            return MorphynController.Instance?.GetField(_registeredEntityName, fieldName) ?? MorphynValue.Null;
        }

        /// <summary>
        /// Set a field value
        /// </summary>
        public void SetField(string fieldName, MorphynValue value)
        {
            MorphynController.Instance?.SetField(_registeredEntityName, fieldName, value);
        }

        public void SetField(string fieldName, bool value) 
            => SetField(fieldName, MorphynValue.FromBool(value));

        public void SetField(string fieldName, double value) 
            => SetField(fieldName, MorphynValue.FromDouble(value));

        public void SetField(string fieldName, float value) 
            => SetField(fieldName, MorphynValue.FromDouble(value));

        public void SetField(string fieldName, string value) 
            => SetField(fieldName, MorphynValue.FromObject(value));

        /// <summary>
        /// Get a field with type conversion
        /// </summary>
        public T Get<T>(string fieldName, T defaultValue) where T : notnull
        {
            var controller = MorphynController.Instance;
            if (controller == null) return defaultValue;
            return controller.Get(_registeredEntityName, fieldName, defaultValue);
        }

        /// <summary>
        /// Watch field changes
        /// </summary>
        public void Watch(string fieldName, Action<MorphynValue, MorphynValue> callback)
        {
            MorphynController.Instance?.Watch(_registeredEntityName, fieldName, callback);
        }

        public void Watch<T>(string fieldName, Action<T, T> callback)
        {
            MorphynController.Instance?.Watch(_registeredEntityName, fieldName, callback);
        }

        /// <summary>
        /// Listen to another entity's events
        /// </summary>
        public void ListenTo(string otherEntityName, string eventName, Action<MorphynValue[]> handler)
        {
            UnityBridge.Instance.AddListener(otherEntityName, eventName, args =>
            {
                var morphArgs = new MorphynValue[args.Length];
                for (int i = 0; i < args.Length; i++)
                    morphArgs[i] = MorphynValue.FromObject(args[i]);
                handler(morphArgs);
            });
        }
    }
}