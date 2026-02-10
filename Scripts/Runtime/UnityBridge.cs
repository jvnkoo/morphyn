using System;
using System.Collections.Generic;

namespace Morphyn.Unity
{
    /// <summary>
    /// Bridge between Morphyn runtime and Unity
    /// Singleton that manages callbacks from Morphyn to Unity
    /// Allows Morphyn scripts to invoke Unity methods via "emit unity(...)"
    /// </summary>
    public class UnityBridge
    {
        private static UnityBridge? _instance;
        
        /// <summary>
        /// Singleton instance of UnityBridge
        /// </summary>
        public static UnityBridge Instance => _instance ??= new UnityBridge();

        private readonly Dictionary<string, Action<object?[]>> _unityCallbacks = new();
        
        private UnityBridge() { }

        /// <summary>
        /// Register a Unity callback that can be called from Morphyn scripts
        /// Example: RegisterCallback("PlaySound", args => AudioSource.PlayOneShot(...))
        /// </summary>
        /// <param name="name">Callback name to use in Morphyn (e.g., "PlaySound")</param>
        /// <param name="callback">Action to execute when callback is invoked</param>
        public void RegisterCallback(string name, Action<object?[]> callback)
        {
            _unityCallbacks[name] = callback;
        }

        /// <summary>
        /// Invoke a registered Unity callback from Morphyn runtime
        /// Called automatically when Morphyn executes "emit unity(name, ...)"
        /// </summary>
        /// <param name="name">Name of the callback to invoke</param>
        /// <param name="args">Arguments to pass to the callback</param>
        public void InvokeUnityCallback(string name, params object?[] args)
        {
            if (_unityCallbacks.TryGetValue(name, out var callback))
            {
                callback(args);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[Morphyn] Unity callback '{name}' not found");
            }
        }

        /// <summary>
        /// Clear all registered callbacks
        /// Useful when reloading scene or cleaning up
        /// </summary>
        public void ClearCallbacks()
        {
            _unityCallbacks.Clear();
        }
    }
}