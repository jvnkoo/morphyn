using System;
using System.Collections.Generic;

namespace Morphyn.Unity
{
    /// <summary>
    /// Bridge between Morphyn runtime and Unity.
    /// Singleton that manages callbacks from Morphyn to Unity
    /// and C# listeners for Morphyn entity events.
    /// </summary>
    public class UnityBridge
    {
        private static UnityBridge? _instance;

        /// <summary>Singleton instance of UnityBridge.</summary>
        public static UnityBridge Instance => _instance ??= new UnityBridge();

        // emit unity("Name", args...) callbacks
        private readonly Dictionary<string, Action<object?[]>> _unityCallbacks = new();

        // On/Off C# listeners for Morphyn entity events
        private readonly Dictionary<(string entity, string eventName), List<Action<object?[]>>> _morphynListeners = new();

        private UnityBridge() { }

        /// <summary>
        /// Register a Unity callback invokable from Morphyn via emit unity("Name", ...).
        /// </summary>
        public void RegisterCallback(string name, Action<object?[]> callback)
        {
            _unityCallbacks[name] = callback;
        }

        /// <summary>
        /// Invoke a registered Unity callback. Called automatically by MorphynRuntime.
        /// </summary>
        public void InvokeUnityCallback(string name, params object?[] args)
        {
            if (_unityCallbacks.TryGetValue(name, out var callback))
                callback(args);
            else
                UnityEngine.Debug.LogWarning($"[Morphyn] Unity callback '{name}' not found");
        }

        /// <summary>
        /// Subscribe a C# handler to a Morphyn entity event.
        /// Handler receives the same args the event was fired with.
        /// </summary>
        public void AddListener(string entityName, string eventName, Action<object?[]> handler)
        {
            var key = (entityName, eventName);
            if (!_morphynListeners.TryGetValue(key, out var list))
            {
                list = new List<Action<object?[]>>();
                _morphynListeners[key] = list;
            }
            if (!list.Contains(handler))
                list.Add(handler);
        }

        /// <summary>
        /// Unsubscribe a C# handler from a Morphyn entity event.
        /// </summary>
        public void RemoveListener(string entityName, string eventName, Action<object?[]> handler)
        {
            var key = (entityName, eventName);
            if (_morphynListeners.TryGetValue(key, out var list))
                list.Remove(handler);
        }

        /// <summary>
        /// Notify all C# listeners for a given entity event. Called by MorphynRuntime via OnEventFired.
        /// </summary>
        public void NotifyListeners(string entityName, string eventName, object?[] args)
        {
            var key = (entityName, eventName);
            if (_morphynListeners.TryGetValue(key, out var list))
            {
                // iterate over a copy — handler may call RemoveListener during iteration
                var copy = list.ToArray();
                foreach (var handler in copy)
                    handler(args);
            }
        }

        /// <summary>
        /// Clear all registered callbacks and listeners.
        /// Call on scene unload or MorphynController.OnDestroy.
        /// </summary>
        public void ClearCallbacks()
        {
            _unityCallbacks.Clear();
            _morphynListeners.Clear();
        }
    }
}