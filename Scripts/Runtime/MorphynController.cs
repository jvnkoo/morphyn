using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Morphyn.Parser;
using Morphyn.Runtime;
using Morphyn.Unity;

/// <summary>
/// Core Morphyn engine - SINGLETON
/// ONE instance manages ALL .morph files in the project
/// </summary>
public class MorphynController : MonoBehaviour
{
    public enum SaveMode
    {
        None,       // Do not save or load
        Auto,       // Automatically save on exit and load on startup
        ManualOnly  // Save/load only when called directly from code
    }

    [Serializable]
    public struct MorphynScriptEntry
    {
        public TextAsset script;
        public SaveMode saveMode;
    }

    private static MorphynController _instance;
    public static MorphynController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MorphynController>();
            }
            return _instance;
        }
    }
    
    [Header("Morphyn Scripts")]
    [Tooltip("Add ALL your .morph files here")]
    [SerializeField] private MorphynScriptEntry[] morphynScripts;
    
    [Header("Settings")]
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private bool enableTick = true;
    [SerializeField] private bool enableHotReload = false;
    [SerializeField] private bool autoSave = false;
    [SerializeField] private string saveFolder = "MorphynData";
    
    private EntityData _context;
    private float _lastTime;
    private List<FileSystemWatcher> _watchers = new();
    private bool _needsReload = false;
    private string _cachedSavePath;
    
    // Optimization: Pre-cache tick entities and reuse tick args buffer
    private List<Entity> _tickEntities = new();
    private readonly object?[] _tickArgsBuffer = new object?[] { 0.0 };
    private object?[] _internalArgsBuffer = new object?[8];
    private object?[] _syncArgsBuffer = new object?[4];

    public EntityData Context => _context;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[Morphyn] Duplicate MorphynController detected! Destroying.");
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Cache persistent path to avoid allocations during runtime
        _cachedSavePath = Path.Combine(Application.persistentDataPath, saveFolder);
    }

    void Start()
    {
        MorphynParser.OnError = msg => Debug.LogError(msg);

        MorphynRuntime.UnityCallback = (name, args) =>
            UnityBridge.Instance.InvokeUnityCallback(name, args);

        MorphynRuntime.OnEventFired = (entityName, eventName, args) =>
            UnityBridge.Instance.NotifyListeners(entityName, eventName, args);

        if (runOnStart)
        {
            LoadAndRun();
            LoadPersistentStates();
            if (enableHotReload) SetupHotReload();
        }
    }

    public void LoadAndRun()
    {
        try
        {
            RegisterUnityCallbacks();

            string combinedCode = "";
            HashSet<string> visitedFiles = new HashSet<string>();

            for (int i = 0; i < morphynScripts.Length; i++)
            {
                var entry = morphynScripts[i];
                var script = entry.script;
                if (script == null) continue;
#if UNITY_EDITOR
                    // In Editor, we can resolve real paths for recursive imports
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(script);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        combinedCode += ResolveImports(Path.GetFullPath(assetPath), visitedFiles) + "\n";
                    }
                    else
                    {
                        combinedCode += script.text + "\n";
                    }
#else
                // In Build, we rely on the pre-loaded TextAsset content
                combinedCode += script.text + "\n";
#endif
            }


            _context = MorphynParser.ParseFile(combinedCode);
            Debug.Log($"[Morphyn] Loaded {_context.Entities.Count} entities");
            
            foreach (var entity in _context.Entities.Values)
            {
                entity.BuildCache();
                if (entity.Events.Any(e => e.Name == "init"))
                {
                    MorphynRuntime.Send(entity, "init");
                }
            }
            
            MorphynRuntime.RunFullCycle(_context);
            
            _tickEntities.Clear();
            foreach (var entity in _context.Entities.Values)
            {
                if (entity.Events.Any(e => e.Name == "tick"))
                {
                    _tickEntities.Add(entity);
                }
            }
            
            _lastTime = Time.time;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Morphyn Error]: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void LoadPersistentStates()
    {
        if (_context == null) return;

        for (int i = 0; i < morphynScripts.Length; i++)
        {
            var entry = morphynScripts[i];
            if (entry.saveMode == SaveMode.Auto && entry.script != null)
            {
                string entityName = entry.script.name; 
                if (_context.Entities.ContainsKey(entityName))
                {
                    LoadState(entityName);
                }
            }
        }
    }

    public void SaveStateByPolicy()
    {
        if (_context == null) return;
        
        bool directoryChecked = false;

        for (int i = 0; i < morphynScripts.Length; i++)
        {
            var entry = morphynScripts[i];
            if (entry.saveMode == SaveMode.Auto && entry.script != null)
            {
                string entityName = entry.script.name;
                if (_context.Entities.TryGetValue(entityName, out var entity))
                {
                    if (!directoryChecked)
                    {
                        if (!Directory.Exists(_cachedSavePath)) Directory.CreateDirectory(_cachedSavePath);
                        directoryChecked = true;
                    }
                    
                    string filePath = Path.Combine(_cachedSavePath, $"{entityName}.morph");
                    MorphynSerializer.SaveEntity(entity, filePath);
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        if (autoSave) SaveStateByPolicy();
    }

    /**
     * Recursive import resolution for Unity
     */
    private string ResolveImports(string absolutePath, HashSet<string> visited)
    {
        if (visited.Contains(absolutePath)) return "";
        visited.Add(absolutePath);

        if (!File.Exists(absolutePath))
        {
            Debug.LogWarning($"[Morphyn] Import not found: {absolutePath}");
            return "";
        }   

        string content = File.ReadAllText(absolutePath);
        string[] lines = content.Split('\n');
        List<string> finalContent = new List<string>(lines.Length);

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
                    
                    finalContent.Add(ResolveImports(fullSubPath, visited));
                    continue; // Skip adding the import line itself
                }
            }
            
            finalContent.Add(line);
        }

        return string.Join("\n", finalContent);
    }

    void SetupHotReload()
    {
#if UNITY_EDITOR
        for (int i = 0; i < morphynScripts.Length; i++)
        {
            var entry = morphynScripts[i];
            if (entry.script == null) continue;
            
            try
            {
                string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(entry.script);
                if (string.IsNullOrEmpty(scriptPath)) continue;
                
                string fullPath = Path.GetFullPath(scriptPath);
                string directory = Path.GetDirectoryName(fullPath);
                string fileName = Path.GetFileName(fullPath);
                
                var watcher = new FileSystemWatcher(directory)
                {
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.LastWrite
                };
                
                watcher.Changed += (s, e) => { _needsReload = true; };
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Morphyn] Hot reload setup failed: {ex.Message}");
            }
        }
#endif
    }

    void Update()
    {
        if (_context == null) return;
        
        try
        {
            if (_needsReload && enableHotReload)
            {
                ReloadLogic();
                _needsReload = false;
            }
            
            if (enableTick)
            {
                float currentTime = Time.time;
                double dt = (currentTime - _lastTime) * 1000f;
                _lastTime = currentTime;
                
                // Optimization: Use pre-cached tick entities and buffer
                _tickArgsBuffer[0] = (double)dt;
                
                int tickCount = _tickEntities.Count;
                for (int i = 0; i < tickCount; i++)
                {
                    MorphynRuntime.Send(_tickEntities[i], "tick", _tickArgsBuffer);
                }
                
                MorphynRuntime.RunFullCycle(_context);
                MorphynRuntime.GarbageCollect(_context);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Morphyn Runtime Error]: {ex.Message}");
        }
    }

    void ReloadLogic()
    {
#if UNITY_EDITOR
        try
        {
            UnityEditor.AssetDatabase.Refresh();
            
            string combinedCode = "";
            for (int i = 0; i < morphynScripts.Length; i++)
            {
                var entry = morphynScripts[i];
                if (entry.script != null) combinedCode += entry.script.text + "\n";
            }
            
            EntityData newData = MorphynParser.ParseFile(combinedCode);

            foreach (var newEntry in newData.Entities)
            {
                string name = newEntry.Key;
                Entity newEntity = newEntry.Value;

                if (_context.Entities.TryGetValue(name, out var existingEntity))
                {
                    existingEntity.Events = newEntity.Events;
                    existingEntity.BuildCache();
                }
                else
                {
                    newEntity.BuildCache();
                    _context.Entities.Add(name, newEntity);
                    if (newEntity.Events.Any(e => e.Name == "init"))
                    {
                        MorphynRuntime.Send(newEntity, "init");
                    }
                    
                    // Optimization: Add new tick entities to cache
                    if (newEntity.Events.Any(e => e.Name == "tick"))
                    {
                        _tickEntities.Add(newEntity);
                    }
                }
            }
            
            MorphynRuntime.RunFullCycle(_context);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Morphyn Hot Reload Error]: {ex.Message}");
        }
#endif
    }

    public object? GetField(string entityName, string fieldName)
    {
        if (_context != null && _context.Entities.TryGetValue(entityName, out var entity))
            if (entity.Fields.TryGetValue(fieldName, out var value))
                return value.ToObject(); // MorphynValue → object?
        return null;
    }

    public void SetField(string entityName, string fieldName, object? value)
    {
        if (_context != null && _context.Entities.TryGetValue(entityName, out var entity))
            entity.Fields[fieldName] = MorphynValue.FromObject(value); // object? → MorphynValue
    }

    public Dictionary<string, object?> GetAllFields(string entityName)
    {
        if (_context != null && _context.Entities.TryGetValue(entityName, out var entity))
        {
            var result = new Dictionary<string, object?>(entity.Fields.Count);
            foreach (var kvp in entity.Fields)
                result[kvp.Key] = kvp.Value.ToObject();
            return result;
        }
        return new Dictionary<string, object?>();
    }

    public void Emit(string entityName, string eventName, params object[] args)
    {
        if (_context != null && _context.Entities.TryGetValue(entityName, out var entity))
        {
            // Optimization: Reuse buffer to avoid List allocation
            if (args.Length > _internalArgsBuffer.Length)
                _internalArgsBuffer = new object?[args.Length];
            for (int i = 0; i < args.Length; i++)
                _internalArgsBuffer[i] = args[i];

            MorphynRuntime.Send(entity, eventName, _internalArgsBuffer);
            MorphynRuntime.RunFullCycle(_context);
        }
    }

    public object? EmitSync(string entityName, string eventName, object? arg0 = null)
    {
        if (_context != null && _context.Entities.TryGetValue(entityName, out var entity))
        {
            _syncArgsBuffer[0] = arg0;
            return MorphynRuntime.ExecuteSync(null, entity, eventName,
                arg0 != null ? _syncArgsBuffer : Array.Empty<object?>(), _context);
        }
        return null;
    }

    /// <summary>
    /// Subscribe a Morphyn entity to another entity's event.
    /// When targetEntity fires targetEvent, subscriberEntity will receive handlerEvent.
    /// </summary>
    /// <param name="subscriberEntityName">The entity that will react</param>
    /// <param name="targetEntityName">The entity to listen to</param>
    /// <param name="targetEvent">The event to listen for</param>
    /// <param name="handlerEvent">The event to fire on the subscriber</param>
    public void Subscribe(string subscriberEntityName, string targetEntityName, string targetEvent, string handlerEvent)
    {
        if (_context == null) return;

        if (!_context.Entities.TryGetValue(subscriberEntityName, out var subscriber))
        {
            Debug.LogWarning($"[Morphyn] Subscribe failed: entity '{subscriberEntityName}' not found.");
            return;
        }

        if (!_context.Entities.TryGetValue(targetEntityName, out var target))
        {
            Debug.LogWarning($"[Morphyn] Subscribe failed: entity '{targetEntityName}' not found.");
            return;
        }

        MorphynRuntime.Subscribe(subscriber, target, targetEvent, handlerEvent);
    }

    /// <summary>
    /// Unsubscribe a Morphyn entity from another entity's event.
    /// </summary>
    /// <param name="subscriberEntityName">The entity to unsubscribe</param>
    /// <param name="targetEntityName">The entity being listened to</param>
    /// <param name="targetEvent">The event being listened for</param>
    /// <param name="handlerEvent">The handler event to remove</param>
    public void Unsubscribe(string subscriberEntityName, string targetEntityName, string targetEvent, string handlerEvent)
    {
        if (_context == null) return;

        if (!_context.Entities.TryGetValue(subscriberEntityName, out var subscriber))
        {
            Debug.LogWarning($"[Morphyn] Unsubscribe failed: entity '{subscriberEntityName}' not found.");
            return;
        }

        if (!_context.Entities.TryGetValue(targetEntityName, out var target))
        {
            Debug.LogWarning($"[Morphyn] Unsubscribe failed: entity '{targetEntityName}' not found.");
            return;
        }

        MorphynRuntime.Unsubscribe(subscriber, target, targetEvent, handlerEvent);
    }

    public void On(string entityName, string eventName, Action<object?[]> handler)
    {
        UnityBridge.Instance.AddListener(entityName, eventName, handler);
    }

    public void Off(string entityName, string eventName, Action<object?[]> handler)
    {
        UnityBridge.Instance.RemoveListener(entityName, eventName, handler);
    }

    public void SaveState()
    {
        if (_context == null) return;
        MorphynSerializer.SaveAllEntities(_context, _cachedSavePath);
    }

    public void LoadState(string entityName)
    {
        if (_context == null || !_context.Entities.TryGetValue(entityName, out var entity)) return;
        string path = Path.Combine(_cachedSavePath, $"{entityName}.morph");
        MorphynSerializer.LoadEntityFields(entity, path);
    }

    public void LoadAllStates()
    {
        if (_context == null) return;
        foreach (var entityName in _context.Entities.Keys)
        {
            LoadState(entityName);
        }
    }

    private void RegisterUnityCallbacks()
    {
        UnityBridge.Instance.RegisterCallback("Log", args =>
        {
            Debug.Log($"[Morphyn]: {string.Join(" ", args)}");
        });
        
        UnityBridge.Instance.RegisterCallback("Move", args =>
        {
            if (args.Length >= 3)
            {
                float x = Convert.ToSingle(args[0]);
                float y = Convert.ToSingle(args[1]);
                float z = Convert.ToSingle(args[2]);
                transform.position += new Vector3(x, y, z);
            }
        });
        
        UnityBridge.Instance.RegisterCallback("Rotate", args =>
        {
            if (args.Length >= 1)
            {
                float angle = Convert.ToSingle(args[0]);
                transform.Rotate(0, angle, 0);
            }
        });
    }

    void OnDestroy()
    {
        for (int i = 0; i < _watchers.Count; i++)
        {
            _watchers[i].EnableRaisingEvents = false;
            _watchers[i].Dispose();
        }
        _watchers.Clear();
        
        MorphynRuntime.UnityCallback = null;
        UnityBridge.Instance.ClearCallbacks();
    }
}