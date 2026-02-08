using System;
using System.Linq;
using Morphyn.Parser;
using static Morphyn.Runtime.MorphynEvaluator;

namespace Morphyn.Runtime
{
    public static class MorphynRuntime
    {
        private static readonly Queue<PendingEvent> _eventQueue = new();

        private static readonly List<object> EmptyArgs = new List<object>(0);

        public static void Send(Entity target, string eventName, List<object>? args = null)
        {
            _eventQueue.Enqueue(new PendingEvent(target, eventName, args ?? EmptyArgs));
        }

        public static void RunFullCycle(EntityData data)
        {
            while (_eventQueue.Count > 0)
            {
                var current = _eventQueue.Dequeue();
                ProcessEvent(data, current);
            }
        }

        private static void ProcessEvent(EntityData data, PendingEvent pending)
        {
            var entity = pending.Target;
            if (!entity.EventCache.TryGetValue(pending.EventName, out var ev)) return;

            var localScope = new Dictionary<string, object>();
            for (int i = 0; i < ev.Parameters.Count; i++)
            {
                var val = i < pending.Args.Count
                    ? pending.Args[i]
                    : throw new Exception(
                        $"Event '{pending.EventName}' expected {ev.Parameters.Count} arguments, but only {pending.Args.Count} provided.");
                localScope[ev.Parameters[i]] = val;
            }

            foreach (var action in ev.Actions)
            {
                // Передаем localScope вместо голого pending.Args
                if (!ExecuteAction(data, entity, action, localScope))
                {
                    break;
                }
            }
        }

        private static bool ExecuteAction(EntityData data, Entity entity, MorphynAction action,
            Dictionary<string, object> localScope)
        {
            switch (action)
            {
                case SetAction set:
                    object value = EvaluateExpression(entity, set.Expression, localScope);

                    if (entity.Fields.ContainsKey(set.TargetField))
                    {
                        entity.Fields[set.TargetField] = value;
                    }

                    return true;

                case CheckAction check:
                    bool passed = EvaluateCheck(entity, check, localScope);

                    if (passed && check.InlineAction != null)
                    {
                        ExecuteAction(data, entity, check.InlineAction, localScope);
                    }
                    
                    return check.InlineAction != null || passed;

                case EmitAction emit:
                    int count = emit.Arguments.Count;
                    List<object> resolvedArgs = new List<object>(count);
                    for (int i = 0; i < count; i++)
                    {
                        resolvedArgs.Add(EvaluateExpression(entity, emit.Arguments[i], localScope));
                    }

                    if (emit.EventName == "log")
                    {
                        Console.WriteLine($"[LOG]: {string.Join(" ", resolvedArgs)}");
                        return true;
                    }

                    Entity? target = entity;
                    if (!string.IsNullOrEmpty(emit.TargetEntityName))
                    {
                        data.Entities.TryGetValue(emit.TargetEntityName, out target);
                    }

                    if (target != null)
                    {
                        Send(target, emit.EventName, resolvedArgs);
                    }

                    return true;

                default:
                    return true;
            }
        }
    }
}