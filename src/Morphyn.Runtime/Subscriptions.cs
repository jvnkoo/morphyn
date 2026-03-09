using System;
using System.Collections.Generic;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class Subscriptions
    {
        // Key: (targetEntityName, targetEventName)
        // Value: list of (subscriber, handlerEvent, handlerArgs)
        // handlerArgs: null = no args passed, non-null = evaluated against subscriber at fire time
        private static readonly Dictionary<(string, string), List<(Entity subscriber, string handler, List<MorphynExpression>? handlerArgs)>>
            _subscriptions = new();

        // Key: (entityName, fieldName)
        // Value: list of (subscriber, handlerEvent) — fired when field value changes
        private static readonly Dictionary<(string, string), List<(Entity subscriber, string handler)>>
            _fieldWatchers = new();

        // Unity-side field change callbacks: (entityName, fieldName) -> list of callbacks(oldValue, newValue)
        private static readonly Dictionary<(string, string), List<Action<MorphynValue, MorphynValue>>>
            _unityFieldCallbacks = new();

        public static void Subscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent, List<MorphynExpression>? handlerArgs = null)
        {
            if (targetEvent == handlerEvent && subscriber == target)
            {
                Console.WriteLine($"[Warning] Possible infinite loop: {subscriber.Name} subscribed {handlerEvent} to itself.");
            }

            var key = (target.Name, targetEvent);
            if (!_subscriptions.TryGetValue(key, out var list))
            {
                list = new List<(Entity, string, List<MorphynExpression>?)>();
                _subscriptions[key] = list;
            }

            for (int i = 0; i < list.Count; i++)
                if (list[i].subscriber == subscriber && list[i].handler == handlerEvent) return;

            list.Add((subscriber, handlerEvent, handlerArgs));
        }

        public static void Unsubscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent)
        {
            var key = (target.Name, targetEvent);
            if (_subscriptions.TryGetValue(key, out var list))
                list.RemoveAll(s => s.subscriber == subscriber && s.handler == handlerEvent);
        }

        public static bool TryGetSubscribers(string entityName, string eventName,
            out List<(Entity subscriber, string handler, List<MorphynExpression>? handlerArgs)> subscribers)
        {
            return _subscriptions.TryGetValue((entityName, eventName), out subscribers!);
        }

        // Subscribe a Morphyn entity to changes of a field on a target entity.
        // When targetEntity.fieldName changes, subscriber will receive handlerEvent(oldValue, newValue).
        public static void WatchField(Entity subscriber, Entity target,
            string fieldName, string handlerEvent)
        {
            var key = (target.Name, fieldName);
            if (!_fieldWatchers.TryGetValue(key, out var list))
            {
                list = new List<(Entity, string)>();
                _fieldWatchers[key] = list;
            }

            for (int i = 0; i < list.Count; i++)
                if (list[i].subscriber == subscriber && list[i].handler == handlerEvent) return;

            list.Add((subscriber, handlerEvent));
        }

        // Remove a field watch subscription.
        public static void UnwatchField(Entity subscriber, Entity target,
            string fieldName, string handlerEvent)
        {
            var key = (target.Name, fieldName);
            if (_fieldWatchers.TryGetValue(key, out var list))
                list.RemoveAll(s => s.subscriber == subscriber && s.handler == handlerEvent);
        }

        // Called by MorphynRuntime when a field is written.
        // Fires all watchers if the value actually changed.
        public static void NotifyFieldChanged(Entity entity, string fieldName,
            MorphynValue oldValue, MorphynValue newValue)
        {
            if (ValuesEqual(oldValue, newValue)) return;

            // Notify Morphyn-side watchers
            var key = (entity.Name, fieldName);
            if (_fieldWatchers.TryGetValue(key, out var morphynList))
            {
                for (int i = 0; i < morphynList.Count; i++)
                {
                    var (subscriber, handler) = morphynList[i];
                    if (!subscriber.IsDestroyed)
                        MorphynRuntime.Send(subscriber, handler, oldValue, newValue);
                }
            }

            // Notify Unity-side callbacks
            if (_unityFieldCallbacks.TryGetValue(key, out var unityList))
            {
                for (int i = 0; i < unityList.Count; i++)
                {
                    try { unityList[i]?.Invoke(oldValue, newValue); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FieldWatch Error] {entity.Name}.{fieldName}: {ex.Message}");
                    }
                }
            }
        }

        // Register a Unity C# callback for when a field changes.
        // Callback receives (oldValue, newValue).
        public static void AddUnityFieldCallback(string entityName, string fieldName,
            Action<MorphynValue, MorphynValue> callback)
        {
            var key = (entityName, fieldName);
            if (!_unityFieldCallbacks.TryGetValue(key, out var list))
            {
                list = new List<Action<MorphynValue, MorphynValue>>();
                _unityFieldCallbacks[key] = list;
            }
            list.Add(callback);
        }

        // Remove a previously registered Unity C# field callback.
        public static void RemoveUnityFieldCallback(string entityName, string fieldName,
            Action<MorphynValue, MorphynValue> callback)
        {
            var key = (entityName, fieldName);
            if (_unityFieldCallbacks.TryGetValue(key, out var list))
                list.Remove(callback);
        }

        public static void RemoveDestroyedSubscribers()
        {
            foreach (var list in _subscriptions.Values)
                list.RemoveAll(s => s.subscriber.IsDestroyed);

            foreach (var list in _fieldWatchers.Values)
                list.RemoveAll(s => s.subscriber.IsDestroyed);
        }

        private static bool ValuesEqual(MorphynValue a, MorphynValue b)
        {
            if (a.Kind != b.Kind) return false;
            return a.Kind switch
            {
                MorphynValueKind.Double => a.NumVal == b.NumVal,
                MorphynValueKind.Bool   => a.BoolVal == b.BoolVal,
                MorphynValueKind.String => (string?)a.ObjVal == (string?)b.ObjVal,
                MorphynValueKind.Null   => true,
                _                      => ReferenceEquals(a.ObjVal, b.ObjVal)
            };
        }
    }
}