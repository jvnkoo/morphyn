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
        private static EntityData? _currentData;
        private static readonly object?[] EmptyArgsArray = Array.Empty<object?>();
        private static readonly HashSet<(Entity, string)> _pendingEventSet = new();
        private const int HASH_SET_THRESHOLD = 20; // Use HashSet only when queue grows

        // Pool for reusing dictionaries to avoid GC pressure during high-frequency events
        private static readonly Stack<Dictionary<string, object?>> _scopePool = new();

        // Pool for reusing object arrays for event arguments
        private static readonly Stack<object?[]> _argArrayPool = new();
        private static bool _needsCleanup = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object?[] RentArgsArray(int size)
        {
            if (size == 0) return EmptyArgsArray;
            if (_argArrayPool.TryPop(out var arr))
            {
                if (arr.Length >= size) return arr;
                _argArrayPool.Push(arr);
            }
            return new object?[Math.Max(8, size)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReturnArgsArray(object?[] arr)
        {
            if (arr == null || arr.Length == 0 || arr == EmptyArgsArray) return;
            Array.Clear(arr, 0, arr.Length);
            _argArrayPool.Push(arr);
        }

        public static void MarkDirty() => _needsCleanup = true;

        private static Dictionary<string, object?> RentScope(int capacity)
        {
            if (_scopePool.TryPop(out var scope))
            {
                scope.Clear();
                return scope;
            }
            return new Dictionary<string, object?>(capacity);
        }

        private static void ReturnScope(Dictionary<string, object?> scope)
        {
            if (_scopePool.Count < 128) // Limit pool size
                _scopePool.Push(scope);
        }

        // Key: (targetEntityName, targetEventName)
        // Value: list of (subscriber, handlerEvent, handlerArgs)
        // handlerArgs: null = no args passed, non-null = evaluated against subscriber at fire time
        private static readonly Dictionary<(string, string), List<(Entity subscriber, string handler, List<MorphynExpression>? handlerArgs)>>
            _subscriptions = new();

        // Tracks active sync call stack per (entity, event) — allows chaining A->B->C, blocks A->A recursion
        private static readonly HashSet<(string entity, string eventName)> _syncCallStack = new();

        // Side-effect emits fired inside sync events go here, drained immediately after sync completes
        private static readonly Queue<PendingEvent> _syncSideEffectQueue = new();
        private static bool _inSyncContext = false;

        public static Action<string, object?[]>? UnityCallback { get; set; }

        public static Action<string, string, object?[]>? OnEventFired { get; set; }

        // Send an event to an entity
        // target: The entity that will receive the event
        // eventName: Name of the event to send
        // args: Optional arguments to pass to the event handler
        public static void Send(Entity target, string eventName, object?[]? args = null)
        {
            int argCount = args?.Length ?? 0;

            // FIX: Queue doesn't have a fast non-allocating struct enumerator in all framework versions.
            // In baseline mode, we use a simple check to avoid any potential iterator allocation.
            if (_eventQueue.Count > 0)
            {
                foreach (var e in _eventQueue)
                {
                    if (e.Target == target && e.EventName == eventName && ArgsEqual(e.Args, e.ArgCount, args))
                        return;
                }
            }

            object?[] borrowedArgs = RentArgsArray(argCount);
            if (args != null)
            {
                for (int i = 0; i < argCount; i++) borrowedArgs[i] = args[i];
            }

            var pendingEvent = new PendingEvent(target, eventName, borrowedArgs, argCount);
            if (_inSyncContext)
                _syncSideEffectQueue.Enqueue(pendingEvent);
            else
                _eventQueue.Enqueue(pendingEvent);

            // FIX: If OnEventFired is subscribed to in Benchmark, this delegate call 
            // can cause a closure allocation of 24 bytes. Ensure it's null during bench.
            OnEventFired?.Invoke(target.Name, eventName, borrowedArgs);

            var key = (target.Name, eventName);
            if (_subscriptions.TryGetValue(key, out var subscribers))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var sub = subscribers[i];
                    if (!sub.subscriber.IsDestroyed)
                    {
                        object?[]? resolvedArgs = null;
                        if (sub.handlerArgs != null)
                        {
                            resolvedArgs = RentArgsArray(sub.handlerArgs.Count);
                            var emptyScope = RentScope(0);
                            try
                            {
                                for (int j = 0; j < sub.handlerArgs.Count; j++)
                                    resolvedArgs[j] = MorphynEvaluator.EvaluateExpression(sub.subscriber, sub.handlerArgs[j], emptyScope, _currentData!);
                            }
                            finally
                            {
                                ReturnScope(emptyScope);
                            }
                        }
                        Send(sub.subscriber, sub.handler, resolvedArgs);
                        if (resolvedArgs != null) ReturnArgsArray(resolvedArgs);
                    }
                }
            }
        }

        private static bool ArgsEqual(object?[] queuedArgs, int queuedCount, object?[]? newArgs)
        {
            int newCount = newArgs?.Length ?? 0;
            if (queuedCount != newCount) return false;
            if (queuedCount == 0) return true;

            for (int i = 0; i < queuedCount; i++)
            {
                object? a = queuedArgs[i];
                object? b = newArgs![i];

                if (ReferenceEquals(a, b)) continue;
                if (a == null || b == null) return false;

                // FIX: Direct comparison to avoid boxing of doubles
                if (a is double da && b is double db)
                {
                    if (Math.Abs(da - db) > 1e-9) return false;
                    continue;
                }

                if (!a.Equals(b)) return false;
            }
            return true;
        }

        public static void RunFullCycle(EntityData data)
        {
            _currentData = data;
            while (_eventQueue.Count > 0)
            {
                var current = _eventQueue.Dequeue();

                if (_pendingEventSet.Count > 0)
                {
                    if (_eventQueue.Count < HASH_SET_THRESHOLD / 2)
                    {
                        _pendingEventSet.Clear();
                    }
                    else
                    {
                        _pendingEventSet.Remove((current.Target, current.EventName));
                    }
                }

                ProcessEvent(data, current);
                ReturnArgsArray(current.Args);
            }
        }

        private static void ProcessEvent(EntityData data, PendingEvent pending)
        {
            var entity = pending.Target;
            if (entity.IsDestroyed || !entity.EventCache.TryGetValue(pending.EventName, out var ev)) return;

            var localScope = RentScope(ev.Parameters.Count);
            for (int i = 0; i < ev.Parameters.Count; i++)
            {
                var val = i < pending.ArgCount
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
            finally
            {
                ReturnScope(localScope);
            }
        }

        // Executes an event synchronously and returns the last assigned value.
        // Chains are allowed (A -> B -> C), only direct recursion (A -> A) is blocked.
        // Regular emits inside sync events are deferred to _syncSideEffectQueue and flushed after.
        public static object? ExecuteSync(Entity callerEntity, Entity targetEntity,
            string eventName, object?[] args, EntityData data)
        {
            var callKey = (targetEntity.Name, eventName);

            if (_syncCallStack.Contains(callKey))
                throw new Exception(
                    $"[Sync Error] Recursive sync call detected: '{targetEntity.Name}.{eventName}' is already in the call stack.");

            if (!targetEntity.EventCache.TryGetValue(eventName, out var ev))
                throw new Exception($"[Sync Error] Event '{eventName}' not found in '{targetEntity.Name}'.");

            bool wasInSyncContext = _inSyncContext;
            _syncCallStack.Add(callKey);
            _inSyncContext = true;
            object? lastAssigned = null;

            var localScope = RentScope(ev.Parameters.Count);
            try
            {
                for (int i = 0; i < ev.Parameters.Count; i++)
                {
                    localScope[ev.Parameters[i]] = i < args.Length
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
                ReturnScope(localScope);
                _syncCallStack.Remove(callKey);
                _inSyncContext = wasInSyncContext;

                if (!wasInSyncContext)
                {
                    while (_syncSideEffectQueue.Count > 0)
                        _eventQueue.Enqueue(_syncSideEffectQueue.Dequeue());
                }
            }

            return lastAssigned;
        }

        public static void Subscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent, List<MorphynExpression>? handlerArgs = null)
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
                list = new List<(Entity, string, List<MorphynExpression>?)>();
                _subscriptions[key] = list;
            }

            bool exists = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].subscriber == subscriber && list[i].handler == handlerEvent)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                list.Add((subscriber, handlerEvent, handlerArgs));
        }

        public static void Unsubscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent)
        {
            var key = (target.Name, targetEvent);
            if (_subscriptions.TryGetValue(key, out var list))
                list.RemoveAll(s => s.subscriber == subscriber && s.handler == handlerEvent);
        }

        private static bool HandleBuiltinEmit(EntityData data, Entity entity, EmitAction emit,
            object?[] resolvedArgs, Dictionary<string, object?> localScope)
        {
            if (emit.TargetEntityName == "self" && emit.EventName == "destroy")
            {
                entity.IsDestroyed = true;
                _needsCleanup = true;
                return true;
            }

            if (emit.EventName == "log")
            {
                for (int i = 0; i < emit.Arguments.Count; i++)
                {
                    var arg = resolvedArgs[i];
                    Console.Write(arg switch
                    {
                        MorphynPool p => "pool[" + p.Values.Count + "]",
                        null => "null",
                        _ => arg.ToString()
                    } + " ");
                }
                Console.WriteLine();
                return true;
            }

            if (emit.EventName == "input")
            {
                string prompt = emit.Arguments.Count > 0 ? resolvedArgs[0]?.ToString() ?? "" : "";
                Console.Write(prompt);
                string? line = Console.ReadLine();

                string targetField = emit.Arguments.Count > 1 ? resolvedArgs[1]?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(targetField))
                {
                    if (double.TryParse(line, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double num))
                    {
                        if (entity.Fields.ContainsKey(targetField)) entity.Fields[targetField] = MorphynValue.FromDouble(num);
                        else localScope[targetField] = num;
                    }
                    else
                    {
                        if (entity.Fields.ContainsKey(targetField)) entity.Fields[targetField] = MorphynValue.FromObject(line);
                        else localScope[targetField] = line;
                    }
                }
                return true;
            }

            if (emit.EventName == "unity")
            {
                if (UnityCallback != null && emit.Arguments.Count > 0)
                {
                    string callbackName = resolvedArgs[0]?.ToString() ?? "";
                    object?[] callbackArgs = EmptyArgsArray;
                    if (emit.Arguments.Count > 1)
                    {
                        callbackArgs = new object?[emit.Arguments.Count - 1];
                        Array.Copy(resolvedArgs, 1, callbackArgs, 0, callbackArgs.Length);
                    }
                    UnityCallback(callbackName, callbackArgs);
                }
                return true;
            }

            return false;
        }

        private static bool HandleEmitRouting(EntityData data, Entity entity, EmitAction emit,
            object?[] resolvedArgs)
        {
            string? targetName = emit.TargetEntityName == "self" ? null : emit.TargetEntityName;

            if (!string.IsNullOrEmpty(targetName) && targetName.Contains('.'))
            {
                var parts = targetName.Split('.');
                string entityName = parts[0];
                string targetPoolName = parts[1];

                if (data.Entities.TryGetValue(entityName, out var extEntity) &&
                    extEntity.Fields.TryGetValue(targetPoolName, out var pVal) && pVal.ObjVal is MorphynPool extPool)
                {
                    if (HandlePoolCommand(extPool, emit.EventName, resolvedArgs, data)) return true;
                }
            }

            if (!string.IsNullOrEmpty(targetName))
            {
                if (entity.Fields.TryGetValue(targetName, out var poolVal) && poolVal.ObjVal is MorphynPool localPool)
                {
                    if (emit.EventName == "each")
                    {
                        string subEventName = resolvedArgs[0]?.ToString() ?? "";
                        object?[] subArgs = EmptyArgsArray;
                        if (emit.Arguments.Count > 1)
                        {
                            int subArgCount = emit.Arguments.Count - 1;
                            subArgs = RentArgsArray(subArgCount);
                            Array.Copy(resolvedArgs, 1, subArgs, 0, subArgCount);
                        }

                        foreach (var item in localPool.Values)
                        {
                            if (item is Entity subE) Send(subE, subEventName, subArgs);
                            else if (item is string eName && data.Entities.TryGetValue(eName, out var extE))
                                Send(extE, subEventName, subArgs);
                        }
                        if (subArgs != EmptyArgsArray) ReturnArgsArray(subArgs);
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
                    if (entity.Fields.ContainsKey(set.TargetField))
                    {
                        var mv = EvaluateToValue(entity, set.Expression, localScope, data);
                        entity.Fields[set.TargetField] = mv;
                        lastAssigned = mv.ToObject();
                    }
                    else
                    {
                        object? value = EvaluateExpression(entity, set.Expression, localScope, data);
                        localScope[set.TargetField] = value;
                        lastAssigned = value;
                    }
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

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fieldVal) &&
                        fieldVal.ObjVal is MorphynPool pool)
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

                case EmitWithReturnIndexAction emitRetIdxSync:
                {
                    int argCount = emitRetIdxSync.Arguments.Count;
                    object?[] resolvedArgs = RentArgsArray(argCount);
                    for (int i = 0; i < argCount; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRetIdxSync.Arguments[i], localScope, data);

                    Entity? target = string.IsNullOrEmpty(emitRetIdxSync.TargetEntityName)
                        ? entity
                        : (data.Entities.TryGetValue(emitRetIdxSync.TargetEntityName, out var e) ? e : null);

                    if (target == null)
                        throw new Exception($"[Sync Error] Entity '{emitRetIdxSync.TargetEntityName}' not found.");

                    object? syncResult = ExecuteSync(entity, target, emitRetIdxSync.EventName, resolvedArgs, data);
                    ReturnArgsArray(resolvedArgs);

                    var idxVal = EvaluateExpression(entity, emitRetIdxSync.IndexExpr, localScope, data);
                    int poolIndex = Convert.ToInt32(idxVal) - 1;

                    if (entity.Fields.TryGetValue(emitRetIdxSync.TargetPoolName, out var poolVal) && poolVal.ObjVal is MorphynPool pool)
                    {
                        if (poolIndex >= 0 && poolIndex < pool.Values.Count)
                            pool.Values[poolIndex] = syncResult;
                        else
                            throw new Exception($"Index {poolIndex + 1} out of bounds for pool '{emitRetIdxSync.TargetPoolName}'");
                    }
                    else
                        throw new Exception($"Target '{emitRetIdxSync.TargetPoolName}' is not a pool or not found.");

                    lastAssigned = syncResult;
                    return true;
                }

                case EmitWithReturnAction emitRetSync:
                {
                    int argCount = emitRetSync.Arguments.Count;
                    object?[] resolvedArgs = RentArgsArray(argCount);
                    for (int i = 0; i < argCount; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRetSync.Arguments[i], localScope, data);

                    Entity? target = string.IsNullOrEmpty(emitRetSync.TargetEntityName)
                        ? entity
                        : (data.Entities.TryGetValue(emitRetSync.TargetEntityName, out var e) ? e : null);

                    if (target == null)
                        throw new Exception($"[Sync Error] Entity '{emitRetSync.TargetEntityName}' not found.");

                    object? syncResult = ExecuteSync(entity, target, emitRetSync.EventName, resolvedArgs, data);
                    ReturnArgsArray(resolvedArgs);

                    if (entity.Fields.ContainsKey(emitRetSync.TargetField))
                        entity.Fields[emitRetSync.TargetField] = MorphynValue.FromObject(syncResult);
                    else
                        localScope[emitRetSync.TargetField] = syncResult;

                    lastAssigned = syncResult;
                    return true;
                }

                case EmitAction emit:
                {
                    int argCount = emit.Arguments.Count;
                    object?[] resolvedArgs = RentArgsArray(argCount);
                    for (int i = 0; i < argCount; i++)
                    {
                        var argExpr = emit.Arguments[i];
                        try
                        {
                            resolvedArgs[i] = EvaluateExpression(entity, argExpr, localScope, data);
                        }
                        catch (Exception) when (emit.EventName == "each" && argExpr is VariableExpression ve)
                        {
                            resolvedArgs[i] = ve.Name;
                        }
                    }

                    if (HandleBuiltinEmit(data, entity, emit, resolvedArgs, localScope))
                    {
                        ReturnArgsArray(resolvedArgs);
                        return true;
                    }
                    HandleEmitRouting(data, entity, emit, resolvedArgs);
                    ReturnArgsArray(resolvedArgs);
                    return true;
                }

                case WhenAction whenAct:
                {
                    if (!data.Entities.TryGetValue(whenAct.TargetEntityName, out var targetEntity))
                    {
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                        return true;
                    }
                    Subscribe(entity, targetEntity, whenAct.TargetEventName, whenAct.HandlerEventName, whenAct.HandlerArgs);
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
                    if (entity.Fields.ContainsKey(set.TargetField))
                        entity.Fields[set.TargetField] = EvaluateToValue(entity, set.Expression, localScope, data);
                    else
                        localScope[set.TargetField] = EvaluateExpression(entity, set.Expression, localScope, data);
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

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fieldVal) &&
                        fieldVal.ObjVal is MorphynPool pool)
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
                    int argCount = emitRetIdx.Arguments.Count;
                    object?[] resolvedArgs = RentArgsArray(argCount);
                    for (int i = 0; i < argCount; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRetIdx.Arguments[i], localScope, data);

                    Entity? target = string.IsNullOrEmpty(emitRetIdx.TargetEntityName)
                        ? entity
                        : (data.Entities.TryGetValue(emitRetIdx.TargetEntityName, out var e) ? e : null);

                    if (target == null)
                        throw new Exception($"[Sync Error] Entity '{emitRetIdx.TargetEntityName}' not found.");

                    object? syncResult = ExecuteSync(entity, target, emitRetIdx.EventName, resolvedArgs, data);
                    ReturnArgsArray(resolvedArgs);

                    var idxVal = EvaluateExpression(entity, emitRetIdx.IndexExpr, localScope, data);
                    int poolIndex = Convert.ToInt32(idxVal) - 1;

                    if (entity.Fields.TryGetValue(emitRetIdx.TargetPoolName, out var poolVal) && poolVal.ObjVal is MorphynPool pool)
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
                    int argCount = emitRet.Arguments.Count;
                    object?[] resolvedArgs = RentArgsArray(argCount);
                    for (int i = 0; i < argCount; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRet.Arguments[i], localScope, data);

                    Entity? target = string.IsNullOrEmpty(emitRet.TargetEntityName)
                        ? entity
                        : (data.Entities.TryGetValue(emitRet.TargetEntityName, out var e) ? e : null);

                    if (target == null)
                        throw new Exception($"[Sync Error] Entity '{emitRet.TargetEntityName}' not found.");

                    object? syncResult = ExecuteSync(entity, target, emitRet.EventName, resolvedArgs, data);
                    ReturnArgsArray(resolvedArgs);

                    if (entity.Fields.ContainsKey(emitRet.TargetField))
                        entity.Fields[emitRet.TargetField] = MorphynValue.FromObject(syncResult);
                    else
                        localScope[emitRet.TargetField] = syncResult;

                    return true;
                }

                case EmitAction emit:
                {
                    int argCount = emit.Arguments.Count;
                    object?[] resolvedArgs = RentArgsArray(argCount);

                    for (int i = 0; i < argCount; i++)
                    {
                        var argExpr = emit.Arguments[i];
                        try
                        {
                            resolvedArgs[i] = EvaluateExpression(entity, argExpr, localScope, data);
                        }
                        catch (Exception) when (emit.EventName == "each" && argExpr is VariableExpression ve)
                        {
                            resolvedArgs[i] = ve.Name;
                        }
                    }

                    if (HandleBuiltinEmit(data, entity, emit, resolvedArgs, localScope))
                    {
                        ReturnArgsArray(resolvedArgs);
                        return true;
                    }
                    HandleEmitRouting(data, entity, emit, resolvedArgs);
                    ReturnArgsArray(resolvedArgs);
                    return true;
                }

                case WhenAction whenAct:
                {
                    if (!data.Entities.TryGetValue(whenAct.TargetEntityName, out var targetEntity))
                    {
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                        return true;
                    }
                    Subscribe(entity, targetEntity, whenAct.TargetEventName, whenAct.HandlerEventName, whenAct.HandlerArgs);
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

        private static bool HandlePoolCommand(MorphynPool pool, string command, object?[] args, EntityData data)
        {
            switch (command)
            {
                case "add":
                    if (args.Length == 0 || args[0] == null)
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
            if (!_needsCleanup) return;

            foreach (var e in data.Entities.Values)
            {
                foreach (var field in e.Fields.Values)
                {
                    if (field.ObjVal is MorphynPool pool)
                    {
                        pool.Values.RemoveAll(item => item is Entity { IsDestroyed: true });
                    }
                }
            }

            foreach (var list in _subscriptions.Values)
                list.RemoveAll(s => s.subscriber.IsDestroyed);

            _needsCleanup = false;
        }
    }
}