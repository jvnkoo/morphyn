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
                if (!ExecuteAction(data, entity, ev, action, pending.Args)) 
                {
                    Console.WriteLine($"[Runtime] Event '{pending.EventName}' stopped.");
                    break;
                }
            }
        }

        private static bool ExecuteAction(EntityData data, Entity entity, Event ev, MorphynAction action, List<object> args)
        {
            switch (action)
            {
                case SetAction set:
                    object value = EvaluateExpression(entity, set.Expression, ev, args);
                    
                    if (entity.Fields.ContainsKey(set.TargetField)) {
                        entity.Fields[set.TargetField] = value;
                        Console.WriteLine($"[Runtime] {entity.Name}.{set.TargetField} -> {value}");
                    }
                    return true;

                case CheckAction check:
                    return EvaluateCheck(entity, check, ev, args);

                case EmitAction emit:
                    var resolvedArgs = emit.Arguments
                        .Select<MorphynExpression, object>(expr => 
                            MorphynEvaluator.EvaluateExpression(entity, expr, ev, args))
                        .ToList();

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