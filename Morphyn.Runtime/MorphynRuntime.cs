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
            
            if (ev == null) return;
            
            foreach (var action in ev.Actions)
            {
                if (!ExecuteAction(data, entity, ev, action, pending.Args)) 
                {
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
                    }
                    return true;

                case CheckAction check:
                    bool passed = EvaluateCheck(entity, check, ev, args);
    
                    if (check.InlineAction != null) {
                        if (passed) {
                            ExecuteAction(data, entity, ev, check.InlineAction, args);
                        }
                        return true; 
                    }

                    return passed; 

                case EmitAction emit:
                    int count = emit.Arguments.Count;
                    List<object> resolvedArgs = new List<object>(count);
                    for (int i = 0; i < count; i++)
                    {
                        resolvedArgs.Add(MorphynEvaluator.EvaluateExpression(entity, emit.Arguments[i], ev, args));
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