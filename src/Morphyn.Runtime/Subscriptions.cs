using System;
using System.Collections.Generic;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    internal static class Subscriptions
    {
        // Key: (targetEntityName, targetEventName)
        // Value: list of (subscriber, handlerEvent, handlerArgs)
        // handlerArgs: null = no args passed, non-null = evaluated against subscriber at fire time
        private static readonly Dictionary<(string, string), List<(Entity subscriber, string handler, List<MorphynExpression>? handlerArgs)>>
            _subscriptions = new();

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

        public static void RemoveDestroyedSubscribers()
        {
            foreach (var list in _subscriptions.Values)
                list.RemoveAll(s => s.subscriber.IsDestroyed);
        }
    }
}