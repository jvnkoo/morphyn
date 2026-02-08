using System;
using System.Collections.Generic;
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
                    : throw new Exception($"Event '{pending.EventName}' expected {ev.Parameters.Count} arguments.");
                localScope[ev.Parameters[i]] = val;
            }

            foreach (var action in ev.Actions)
            {
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
                        entity.Fields[set.TargetField] = value;
                    else
                        localScope[set.TargetField] = value;

                    return true;

                case CheckAction check:
                {
                    object result = EvaluateExpression(entity, check.Condition, localScope);
                    bool passed = result is bool b && b;
                    
                    if (passed)
                    {
                        if (check.InlineAction != null)
                        {
                            return ExecuteAction(data, entity, check.InlineAction, localScope);
                        }
                        return true; 
                    }
                    else
                    {
                        if (check.InlineAction != null)
                        {
                            return true; 
                        }
                        return false;
                    }
                }
                
                case SetIndexAction setIdx:
                {
                    object newValue = EvaluateExpression(entity, setIdx.ValueExpr, localScope);
                    int index = Convert.ToInt32(EvaluateExpression(entity, setIdx.IndexExpr, localScope)) - 1;

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fieldObj) && fieldObj is MorphynPool pool)
                    {
                        if (index >= 0 && index < pool.Values.Count)
                        {
                            pool.Values[index] = newValue;
                        }
                        else
                        {
                            throw new Exception($"Index {index + 1} out of bounds for pool '{setIdx.TargetPoolName}'");
                        }
                    }
                    else
                    {
                        throw new Exception($"Target '{setIdx.TargetPoolName}' is not a pool or not found.");
                    }
                    return true;
                }
                
                case BlockAction block:
                    foreach (var subAction in block.Actions)
                    {
                        if (!ExecuteAction(data, entity, subAction, localScope))
                            return false;
                    }
                    return true;

                case EmitAction emit:
                {
                    List<object> resolvedArgs = emit.Arguments
                        .Select(a => EvaluateExpression(entity, a, localScope))
                        .ToList();

                    if (emit.EventName == "log")
                    {
                        var logParts = resolvedArgs.Select(arg => arg is MorphynPool p 
                            ? "pool[" + string.Join(", ", p.Values) + "]" 
                            : arg?.ToString() ?? "null");
                        Console.WriteLine(string.Join(" ", logParts));
                        return true;
                    }
    
                    if (!string.IsNullOrEmpty(emit.TargetEntityName) && 
                        entity.Fields.TryGetValue(emit.TargetEntityName, out var poolObj) && 
                        poolObj is MorphynPool pool && 
                        HandlePoolCommand(pool, emit.EventName, resolvedArgs))
                    {
                        return true;
                    }

                    Entity? target = null;
                    if (string.IsNullOrEmpty(emit.TargetEntityName)) {
                        target = entity; 
                    } else {
                        data.Entities.TryGetValue(emit.TargetEntityName, out target);
                    }

                    if (target != null) {
                        Send(target, emit.EventName, resolvedArgs);
                    } else {
                        Console.WriteLine($"[ERROR] Target entity '{emit.TargetEntityName}' not found for event '{emit.EventName}'");
                    }

                    return true;
                }

                default:
                    return true;
            }
        }
        
        private static bool HandlePoolCommand(MorphynPool pool, string command, List<object> args)
        {
            switch (command)
            {
                case "add":
                    pool.Values.Add(args[0]);
                    return true;
                case "push": 
                    pool.Values.Insert(0, args[0]);
                    return true;
                case "insert": 
                    pool.Values.Insert(Convert.ToInt32(args[0]) - 1, args[1]);
                    return true;
                case "remove_at": 
                    int idxRem = Convert.ToInt32(args[0]) - 1;
                    if (idxRem >= 0 && idxRem < pool.Values.Count)
                        pool.Values.RemoveAt(idxRem);
                    return true;
                case "remove": 
                    pool.Values.Remove(args[0]);
                    return true;
                case "pop":
                    if (pool.Values.Count > 0) pool.Values.RemoveAt(pool.Values.Count - 1);
                    return true;
                case "shift":
                    if (pool.Values.Count > 0) pool.Values.RemoveAt(0);
                    return true;
                case "swap": 
                    int i1 = Convert.ToInt32(args[0]) - 1;
                    int i2 = Convert.ToInt32(args[1]) - 1;
                    if (i1 >= 0 && i1 < pool.Values.Count && i2 >= 0 && i2 < pool.Values.Count)
                    {
                        var temp = pool.Values[i1];
                        pool.Values[i1] = pool.Values[i2];
                        pool.Values[i2] = temp;
                    }
                    return true;
                case "clear": 
                    pool.Values.Clear();
                    return true;
                default:
                    return false; 
            }
        }
    }
}