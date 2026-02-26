using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Morphyn.Parser;
using static Morphyn.Runtime.MorphynEvaluator;

namespace Morphyn.Runtime
{
    // Event processing and entity lifecycle management
    public static class MorphynRuntime
    {
        private static readonly Queue<PendingEvent> _eventQueue = new();
        private static readonly List<object?> EmptyArgs = new List<object?>(0);
        private static readonly HashSet<(Entity, string)> _pendingEventSet = new();
        private const int HASH_SET_THRESHOLD = 20; // Use HashSet only when queue grows

        private static readonly Dictionary<(string, string), List<(Entity subscriber, string handler)>>
            _subscriptions = new();

        // Prevents nested sync calls to eliminate recursion
        private static bool _inSyncCall = false;

        public static Action<string, object?[]>? UnityCallback { get; set; }

        public static Action<string, string, object?[]>? OnEventFired { get; set; }

        // Send an event to an entity
        // target: The entity that will receive the event
        // eventName: Name of the event to send
        // args: Optional arguments to pass to the event handler
        public static void Send(Entity target, string eventName, List<object?>? args = null)
        {
            if (_eventQueue.Count < HASH_SET_THRESHOLD)
            {
                if (_eventQueue.Any(e => e.EventName == eventName && e.Target == target
                    && ArgsEqual(e.Args, args)))
                    return;
            }
            else
            {
                if (_eventQueue.Any(e => e.EventName == eventName && e.Target == target
                    && ArgsEqual(e.Args, args)))
                    return;
            }

            _eventQueue.Enqueue(new PendingEvent(target, eventName, args ?? EmptyArgs));

            OnEventFired?.Invoke(target.Name, eventName, args?.ToArray() ?? Array.Empty<object?>());

            var key = (target.Name, eventName);
            if (_subscriptions.TryGetValue(key, out var subscribers))
            {
                foreach (var (subscriber, handler) in subscribers)
                {
                    if (!subscriber.IsDestroyed)
                        Send(subscriber, handler, args);
                }
            }
        }

        private static bool ArgsEqual(List<object?> a, List<object?>? b)
        {
            if (b == null) return a.Count == 0;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!Equals(a[i], b[i])) return false;
            return true;
        }

        public static void RunFullCycle(EntityData data)
        {
            while (_eventQueue.Count > 0)
            {
                var current = _eventQueue.Dequeue();

                // Clean up HashSet when queue becomes small again
                if (_eventQueue.Count < HASH_SET_THRESHOLD / 2 && _pendingEventSet.Count > 0)
                {
                    _pendingEventSet.Clear();
                }
                else if (_pendingEventSet.Count > 0)
                {
                    _pendingEventSet.Remove((current.Target, current.EventName));
                }

                ProcessEvent(data, current);
            }

            // Final cleanup
            if (_pendingEventSet.Count > 0)
                _pendingEventSet.Clear();
        }

        private static void ProcessEvent(EntityData data, PendingEvent pending)
        {
            var entity = pending.Target;
            if (!entity.EventCache.TryGetValue(pending.EventName, out var ev)) return;

            var localScope = new Dictionary<string, object?>(ev.Parameters.Count);
            for (int i = 0; i < ev.Parameters.Count; i++)
            {
                var val = i < pending.Args.Count
                    ? pending.Args[i]
                    : throw new Exception($"Event '{pending.EventName}' expected {ev.Parameters.Count} arguments.");
                localScope[ev.Parameters[i]] = val;
            }

            try
            {
                foreach (var action in ev.Actions)
                {
                    if (!ExecuteAction(data, entity, action, localScope))
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Runtime Error] Entity '{entity.Name}', event '{pending.EventName}': {ex.Message}");
            }
        }

        // Executes an event synchronously and returns the last assigned value.
        // Nested sync calls are blocked to prevent recursion.
        public static object? ExecuteSync(Entity callerEntity, Entity targetEntity,
            string eventName, List<object?> args, EntityData data)
        {
            if (_inSyncCall)
                throw new Exception(
                    $"[Sync Error] Nested sync calls not allowed. Cannot use 'emit ... -> field' inside a sync event '{eventName}'.");

            if (!targetEntity.EventCache.TryGetValue(eventName, out var ev))
                throw new Exception($"[Sync Error] Event '{eventName}' not found in '{targetEntity.Name}'.");

            _inSyncCall = true;
            object? lastAssigned = null;

            try
            {
                var localScope = new Dictionary<string, object?>(ev.Parameters.Count);
                for (int i = 0; i < ev.Parameters.Count; i++)
                {
                    localScope[ev.Parameters[i]] = i < args.Count
                        ? args[i]
                        : throw new Exception($"[Sync Error] Event '{eventName}' expected {ev.Parameters.Count} arguments.");
                }

                foreach (var action in ev.Actions)
                {
                    if (!ExecuteSyncAction(data, targetEntity, action, localScope, ref lastAssigned))
                        break;
                }
            }
            finally
            {
                _inSyncCall = false;
            }

            return lastAssigned;
        }

        public static void Subscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent)
        {
            if (subscriber == target)
            {
                Console.WriteLine(
                    $"[Subscription Error] Entity '{subscriber.Name}' cannot subscribe to its own events.");
                return;
            }

            var key = (target.Name, targetEvent);
            if (!_subscriptions.TryGetValue(key, out var list))
            {
                list = new List<(Entity, string)>();
                _subscriptions[key] = list;
            }

            if (!list.Any(s => s.subscriber == subscriber && s.handler == handlerEvent))
                list.Add((subscriber, handlerEvent));
        }

        public static void Unsubscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent)
        {
            var key = (target.Name, targetEvent);
            if (_subscriptions.TryGetValue(key, out var list))
                list.RemoveAll(s => s.subscriber == subscriber && s.handler == handlerEvent);
        }

        private static bool HandleBuiltinEmit(EntityData data, Entity entity, EmitAction emit,
            List<object?> resolvedArgs, Dictionary<string, object?> localScope)
        {
            if (emit.TargetEntityName == "self" && emit.EventName == "destroy")
            {
                entity.IsDestroyed = true;
                return true;
            }

            if (emit.EventName == "log")
            {
                var logParts = resolvedArgs.Select(arg => arg switch
                {
                    MorphynPool p => "pool[" + string.Join(", ", p.Values) + "]",
                    null => "null",
                    _ => arg.ToString()
                });
                Console.WriteLine(string.Join(" ", logParts));
                return true;
            }

            if (emit.EventName == "input")
            {
                string prompt = resolvedArgs.Count > 0 ? resolvedArgs[0]?.ToString() ?? "" : "";
                Console.Write(prompt);
                string? line = Console.ReadLine();

                string targetField = resolvedArgs.Count > 1 ? resolvedArgs[1]?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(targetField))
                {
                    if (double.TryParse(line, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double num))
                    {
                        if (entity.Fields.ContainsKey(targetField)) entity.Fields[targetField] = num;
                        else localScope[targetField] = num;
                    }
                    else
                    {
                        if (entity.Fields.ContainsKey(targetField)) entity.Fields[targetField] = line;
                        else localScope[targetField] = line;
                    }
                }
                return true;
            }

            if (emit.EventName == "unity")
            {
                if (UnityCallback != null && resolvedArgs.Count > 0)
                {
                    string callbackName = resolvedArgs[0]?.ToString() ?? "";
                    object?[] callbackArgs = resolvedArgs.Count > 1
                        ? resolvedArgs.GetRange(1, resolvedArgs.Count - 1).ToArray()
                        : Array.Empty<object?>();
                    UnityCallback(callbackName, callbackArgs);
                }
                return true;
            }

            return false;
        }

        private static bool HandleEmitRouting(EntityData data, Entity entity, EmitAction emit,
            List<object?> resolvedArgs)
        {
            string? targetName = emit.TargetEntityName;

            if (!string.IsNullOrEmpty(targetName) && targetName.Contains('.'))
            {
                var parts = targetName.Split('.');
                string entityName = parts[0];
                string targetPoolName = parts[1];

                if (data.Entities.TryGetValue(entityName, out var extEntity) &&
                    extEntity.Fields.TryGetValue(targetPoolName, out var pObj) && pObj is MorphynPool extPool)
                {
                    if (HandlePoolCommand(extPool, emit.EventName, resolvedArgs, data)) return true;
                }
            }

            if (!string.IsNullOrEmpty(targetName))
            {
                if (entity.Fields.TryGetValue(targetName, out var poolObj) && poolObj is MorphynPool localPool)
                {
                    if (emit.EventName == "each")
                    {
                        string subEventName = resolvedArgs[0]?.ToString() ?? "";
                        List<object?> subArgs = resolvedArgs.Skip(1).ToList();
                        foreach (var item in localPool.Values)
                        {
                            if (item is Entity subE) Send(subE, subEventName, subArgs);
                            else if (item is string eName && data.Entities.TryGetValue(eName, out var extE))
                                Send(extE, subEventName, subArgs);
                        }
                        return true;
                    }

                    if (HandlePoolCommand(localPool, emit.EventName, resolvedArgs, data)) return true;
                }
            }

            Entity? emitTarget = string.IsNullOrEmpty(targetName)
                ? entity
                : (data.Entities.TryGetValue(targetName, out var e) ? e : null);

            if (emitTarget != null)
                Send(emitTarget, emit.EventName, resolvedArgs);
            else
                Console.WriteLine($"[ERROR] Target entity '{targetName}' not found for event '{emit.EventName}'");

            return true;
        }

        // Executes an action inside a sync event.
        // Regular emit is allowed (queued for later). Only emit X() -> field is forbidden to prevent recursion.
        private static bool ExecuteSyncAction(EntityData data, Entity entity, MorphynAction action,
            Dictionary<string, object?> localScope, ref object? lastAssigned)
        {
            switch (action)
            {
                case SetAction set:
                {
                    object? value = EvaluateExpression(entity, set.Expression, localScope, data);

                    if (entity.Fields.ContainsKey(set.TargetField))
                        entity.Fields[set.TargetField] = value;
                    else
                        localScope[set.TargetField] = value;

                    lastAssigned = value;
                    return true;
                }

                case CheckAction check:
                {
                    object? result = EvaluateExpression(entity, check.Condition, localScope, data);
                    bool passed = Convert.ToBoolean(result);

                    if (!passed)
                        return check.InlineAction != null;

                    if (check.InlineAction != null)
                        return ExecuteSyncAction(data, entity, check.InlineAction, localScope, ref lastAssigned);

                    return true;
                }

                case SetIndexAction setIdx:
                {
                    object? newValue = EvaluateExpression(entity, setIdx.ValueExpr, localScope, data);
                    var indexResult = EvaluateExpression(entity, setIdx.IndexExpr, localScope, data);

                    if (indexResult == null)
                        throw new Exception($"Index expression evaluated to null for pool '{setIdx.TargetPoolName}'");

                    int index = Convert.ToInt32(indexResult) - 1;

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fieldObj) &&
                        fieldObj is MorphynPool pool)
                    {
                        if (index >= 0 && index < pool.Values.Count)
                            pool.Values[index] = newValue;
                        else
                            throw new Exception($"Index {index + 1} out of bounds for pool '{setIdx.TargetPoolName}'");
                    }
                    else
                    {
                        throw new Exception($"Target '{setIdx.TargetPoolName}' is not a pool or not found.");
                    }

                    lastAssigned = newValue;
                    return true;
                }

                case BlockAction block:
                {
                    foreach (var sub in block.Actions)
                    {
                        if (!ExecuteSyncAction(data, entity, sub, localScope, ref lastAssigned))
                            return false;
                    }
                    return true;
                }

                case EmitWithReturnIndexAction:
                case EmitWithReturnAction:
                    throw new Exception(
                        "[Sync Error] Nested sync calls not allowed. Cannot use 'emit ... -> field' inside a sync event.");

                case EmitAction emit:
                {
                    List<object?> resolvedArgs = new List<object?>(emit.Arguments.Count);
                    foreach (var argExpr in emit.Arguments)
                    {
                        try
                        {
                            resolvedArgs.Add(EvaluateExpression(entity, argExpr, localScope, data));
                        }
                        catch (Exception) when (emit.EventName == "each" && argExpr is VariableExpression ve)
                        {
                            resolvedArgs.Add(ve.Name);
                        }
                    }

                    if (HandleBuiltinEmit(data, entity, emit, resolvedArgs, localScope)) return true;
                    HandleEmitRouting(data, entity, emit, resolvedArgs);
                    return true;
                }

                case WhenAction whenAct:
                {
                    if (!data.Entities.TryGetValue(whenAct.TargetEntityName, out var targetEntity))
                    {
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                        return true;
                    }
                    Subscribe(entity, targetEntity, whenAct.TargetEventName, whenAct.HandlerEventName);
                    return true;
                }

                case UnwhenAction unwhenAct:
                {
                    if (!data.Entities.TryGetValue(unwhenAct.TargetEntityName, out var targetEntity))
                    {
                        Console.WriteLine($"[Subscription Error] Entity '{unwhenAct.TargetEntityName}' not found.");
                        return true;
                    }
                    Unsubscribe(entity, targetEntity, unwhenAct.TargetEventName, unwhenAct.HandlerEventName);
                    return true;
                }

                default:
                    return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ExecuteAction(EntityData data, Entity entity, MorphynAction action,
            Dictionary<string, object?> localScope)
        {
            switch (action)
            {
                case SetAction set:
                    object? value = EvaluateExpression(entity, set.Expression, localScope, data);

                    if (entity.Fields.ContainsKey(set.TargetField))
                        entity.Fields[set.TargetField] = value;
                    else
                        localScope[set.TargetField] = value;

                    return true;

                case CheckAction check:
                {
                    object? result = EvaluateExpression(entity, check.Condition, localScope, data);
                    bool passed = Convert.ToBoolean(result);

                    if (!passed)
                    {
                        return check.InlineAction != null;
                    }

                    if (check.InlineAction != null)
                        return ExecuteAction(data, entity, check.InlineAction, localScope);

                    return true;
                }

                case SetIndexAction setIdx:
                {
                    object? newValue = EvaluateExpression(entity, setIdx.ValueExpr, localScope, data);
                    var indexResult = EvaluateExpression(entity, setIdx.IndexExpr, localScope, data);

                    if (indexResult == null)
                        throw new Exception($"Index expression evaluated to null for pool '{setIdx.TargetPoolName}'");

                    int index = Convert.ToInt32(indexResult) - 1;

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fieldObj) &&
                        fieldObj is MorphynPool pool)
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

                case EmitWithReturnIndexAction emitRetIdx:
                {
                    List<object?> resolvedArgs = new List<object?>(emitRetIdx.Arguments.Count);
                    foreach (var argExpr in emitRetIdx.Arguments)
                        resolvedArgs.Add(EvaluateExpression(entity, argExpr, localScope, data));

                    Entity? target = string.IsNullOrEmpty(emitRetIdx.TargetEntityName)
                        ? entity
                        : (data.Entities.TryGetValue(emitRetIdx.TargetEntityName, out var e) ? e : null);

                    if (target == null)
                        throw new Exception($"[Sync Error] Entity '{emitRetIdx.TargetEntityName}' not found.");

                    object? syncResult = ExecuteSync(entity, target, emitRetIdx.EventName, resolvedArgs, data);

                    var idxVal = EvaluateExpression(entity, emitRetIdx.IndexExpr, localScope, data);
                    int poolIndex = Convert.ToInt32(idxVal) - 1;

                    if (entity.Fields.TryGetValue(emitRetIdx.TargetPoolName, out var poolObj) && poolObj is MorphynPool pool)
                    {
                        if (poolIndex >= 0 && poolIndex < pool.Values.Count)
                            pool.Values[poolIndex] = syncResult;
                        else
                            throw new Exception($"Index {poolIndex + 1} out of bounds for pool '{emitRetIdx.TargetPoolName}'");
                    }
                    else
                        throw new Exception($"Target '{emitRetIdx.TargetPoolName}' is not a pool or not found.");

                    return true;
                }

                case EmitWithReturnAction emitRet:
                {
                    List<object?> resolvedArgs = new List<object?>(emitRet.Arguments.Count);
                    foreach (var argExpr in emitRet.Arguments)
                        resolvedArgs.Add(EvaluateExpression(entity, argExpr, localScope, data));

                    Entity? target = string.IsNullOrEmpty(emitRet.TargetEntityName)
                        ? entity
                        : (data.Entities.TryGetValue(emitRet.TargetEntityName, out var e) ? e : null);

                    if (target == null)
                        throw new Exception($"[Sync Error] Entity '{emitRet.TargetEntityName}' not found.");

                    object? syncResult = ExecuteSync(entity, target, emitRet.EventName, resolvedArgs, data);

                    if (entity.Fields.ContainsKey(emitRet.TargetField))
                        entity.Fields[emitRet.TargetField] = syncResult;
                    else
                        localScope[emitRet.TargetField] = syncResult;

                    return true;
                }

                case EmitAction emit:
                {
                    List<object?> resolvedArgs = new List<object?>(emit.Arguments.Count);

                    foreach (var argExpr in emit.Arguments)
                    {
                        try
                        {
                            resolvedArgs.Add(EvaluateExpression(entity, argExpr, localScope, data));
                        }
                        catch (Exception) when (emit.EventName == "each" && argExpr is VariableExpression ve)
                        {
                            resolvedArgs.Add(ve.Name);
                        }
                    }

                    if (HandleBuiltinEmit(data, entity, emit, resolvedArgs, localScope)) return true;
                    HandleEmitRouting(data, entity, emit, resolvedArgs);
                    return true;
                }

                case WhenAction whenAct:
                {
                    if (!data.Entities.TryGetValue(whenAct.TargetEntityName, out var targetEntity))
                    {
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                        return true;
                    }
                    Subscribe(entity, targetEntity, whenAct.TargetEventName, whenAct.HandlerEventName);
                    return true;
                }

                case UnwhenAction unwhenAct:
                {
                    if (!data.Entities.TryGetValue(unwhenAct.TargetEntityName, out var targetEntity))
                    {
                        Console.WriteLine($"[Subscription Error] Entity '{unwhenAct.TargetEntityName}' not found.");
                        return true;
                    }
                    Unsubscribe(entity, targetEntity, unwhenAct.TargetEventName, unwhenAct.HandlerEventName);
                    return true;
                }

                default:
                    return true;
            }
        }

        private static bool HandlePoolCommand(MorphynPool pool, string command, List<object?> args, EntityData data)
        {
            switch (command)
            {
                case "add":
                    if (args.Count == 0 || args[0] == null)
                        throw new Exception("Pool add command requires a non-null argument");

                    string typeName = args[0].ToString()!;
                    if (data.Entities.TryGetValue(typeName, out var prototype))
                    {
                        var newEntity = prototype.Clone();
                        pool.Values.Add(newEntity);
                        Send(newEntity, "init");
                    }
                    else
                    {
                        pool.Values.Add(args[0]);
                    }

                    return true;
                case "push":
                    pool.Values.Insert(0, args[0]);
                    return true;
                case "insert":
                    if (args[0] == null)
                        throw new Exception("Insert index cannot be null");
                    pool.Values.Insert(Convert.ToInt32(args[0]) - 1, args[1]);
                    return true;
                case "remove_at":
                    if (args[0] == null)
                        throw new Exception("Remove_at index cannot be null");
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
                    if (args[0] == null || args[1] == null)
                        throw new Exception("Swap indices cannot be null");
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

        public static void GarbageCollect(EntityData data)
        {
            foreach (var e in data.Entities.Values)
            {
                foreach (var field in e.Fields.Values)
                {
                    if (field is MorphynPool pool)
                    {
                        pool.Values.RemoveAll(item => item is Entity { IsDestroyed: true });
                    }
                }
            }

            foreach (var list in _subscriptions.Values)
                list.RemoveAll(s => s.subscriber.IsDestroyed);
        }
    }
}