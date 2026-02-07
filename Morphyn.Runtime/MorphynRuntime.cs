using System;
using System.Linq;
using Morphyn.Parser;
using static Morphyn.Runtime.MorphynEvaluator;

namespace Morphyn.Runtime
{
    public static class MorphynRuntime
    {
        private static readonly Queue<PendingEvent> _eventQueue = new();

        public static void Send(Entity target, string eventName, List<object>? args = null)
        {
            _eventQueue.Enqueue(new PendingEvent(target, eventName, args ?? new()));
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
            var ev = entity.Events.FirstOrDefault(e => e.Name == pending.EventName);

            if (ev == null) return;
            
            Console.WriteLine($"\n[Runtime] Processing '{pending.EventName}' on {entity.Name}");

            foreach (var action in ev.Actions)
            {
                if (!ExecuteAction(data, entity, action))
                {
                    Console.WriteLine($"[Runtime] Event '{pending.EventName}' stopped.");
                    break;
                }
            }
        }

        private static bool ExecuteAction(EntityData data, Entity entity, MorphynAction action)
        {
            switch (action)
            {
                case EmitAction emit:
                    if (emit.EventName == "log")
                    {
                        Console.WriteLine($"[LOG]: {string.Join(" ", emit.Arguments)}");
                        return true;
                    }

                    Entity target = entity;
                    if (!string.IsNullOrEmpty(emit.TargetEntityName))
                    {
                        if (!data.Entities.TryGetValue(emit.TargetEntityName, out target))
                        {
                            Console.WriteLine($"[Runtime Error] Target '{emit.TargetEntityName}' not found.");
                            return true;
                        }
                    }
                    
                    Send(target, emit.EventName, emit.Arguments);
                    return true;
                case CheckAction check:
                    return EvaluateCheck(data, entity, check.Expression);
                
                default:
                    return true;
            }
        }
    }
}