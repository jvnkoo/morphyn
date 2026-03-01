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
        private const int HASH_SET_THRESHOLD = 20;

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
            if (_scopePool.Count < 128)
                _scopePool.Push(scope);
        }

        // Key: (targetEntityName, targetEventName)
        // Value: list of (subscriber, handlerEvent, handlerArgs)
        // handlerArgs: null = no args passed, non-null = evaluated against subscriber at fire time
        private static readonly Dictionary<(string, string), List<(Entity subscriber, string handler, List<MorphynExpression>? handlerArgs)>>
            _subscriptions = new();

        private static readonly HashSet<(string entity, string eventName)> _syncCallStack = new();

        // Side-effect emits fired inside sync events go here, drained immediately after sync completes
        private static readonly Queue<PendingEvent> _syncSideEffectQueue = new();
        private static bool _inSyncContext = false;

        public static Action<string, object?[]>? UnityCallback { get; set; }

        public static Action<string, string, object?[]>? OnEventFired { get; set; }

        public static void Send(Entity target, string eventName, object?[]? args = null)
        {
            int argCount = args?.Length ?? 0;

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
                        _pendingEventSet.Clear();
                    else
                        _pendingEventSet.Remove((current.Target, current.EventName));
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
                localScope[ev.Parameters[i]] = i < pending.ArgCount
                    ? pending.Args[i]
                    : throw new Exception($"Event '{pending.EventName}' expected {ev.Parameters.Count} arguments.");
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

        // A pending action in the iterative sync execution engine.
        // When ReturnTarget is set and the action is an EmitWithReturn, the completed child frame
        // writes its result back into ParentScope[ReturnTarget] before the parent resumes.
        private struct ActionItem
        {
            public MorphynAction Action;
            public Entity Entity;
            public Dictionary<string, object?> Scope;
            // Non-null only for EmitWithReturnAction: field to write result into when child completes
            public string? ReturnField;
            // True = write result into entity.Fields[ReturnField], false = write into Scope[ReturnField]
            public bool ReturnToEntityField;
        }

        // Each SyncFrame represents one active event invocation on the explicit call stack.
        // ActionQueue contains the remaining actions to execute for this frame.
        // When the queue empties the frame is popped and the result propagates to the parent.
        private struct SyncFrame
        {
            public Entity Entity;
            public Event Event;
            public Dictionary<string, object?> Scope;
            public Queue<ActionItem> ActionQueue;
            // Where to write lastAssigned when this frame finishes (into parent scope or entity field)
            public string? ReturnField;
            public Dictionary<string, object?>? ParentScope;
            public Entity? ParentEntity;
        }

        // Resolves "self", empty string, and named entities uniformly.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Entity ResolveTarget(EntityData data, Entity current, string? name)
        {
            if (string.IsNullOrEmpty(name) || name == "self") return current;
            if (data.Entities.TryGetValue(name, out var e)) return e;
            throw new Exception($"[Sync Error] Entity '{name}' not found.");
        }

        // Flattens an action list into an ActionItem queue, inlining BlockAction children
        // so the main loop never needs to recurse into blocks.
        private static void EnqueueActions(Queue<ActionItem> queue, IList<MorphynAction> actions,
            Entity entity, Dictionary<string, object?> scope)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action is BlockAction block)
                    EnqueueActions(queue, block.Actions, entity, scope);
                else
                    queue.Enqueue(new ActionItem { Action = action, Entity = entity, Scope = scope });
            }
        }

        // Fully iterative sync execution. The C# call stack depth is O(1) regardless of how deeply
        // Morphyn events recurse into each other — all frames live on the heap-allocated callStack.
        public static object? ExecuteSync(Entity callerEntity, Entity targetEntity,
            string eventName, object?[] args, EntityData data)
        {
            if (!targetEntity.EventCache.TryGetValue(eventName, out var firstEv))
                throw new Exception($"[Sync Error] Event '{eventName}' not found in '{targetEntity.Name}'.");

            bool wasInSyncContext = _inSyncContext;
            _inSyncContext = true;
            object? lastAssigned = null;

            var callStack = new Stack<SyncFrame>();

            var firstScope = RentScope(firstEv.Parameters.Count);
            try
            {
                for (int i = 0; i < firstEv.Parameters.Count; i++)
                {
                    firstScope[firstEv.Parameters[i]] = i < args.Length
                        ? args[i]
                        : throw new Exception($"[Sync Error] Event '{eventName}' expected {firstEv.Parameters.Count} arguments.");
                }

                var firstQueue = new Queue<ActionItem>();
                EnqueueActions(firstQueue, firstEv.Actions, targetEntity, firstScope);

                callStack.Push(new SyncFrame
                {
                    Entity = targetEntity,
                    Event = firstEv,
                    Scope = firstScope,
                    ActionQueue = firstQueue,
                    ReturnField = null,
                    ParentScope = null,
                    ParentEntity = null
                });

                while (callStack.Count > 0)
                {
                    if (callStack.Count > 100_000_000)
                        throw new Exception("[Sync Error] Call stack depth limit reached (100kk frames).");

                    var frame = callStack.Peek();

                    if (frame.ActionQueue.Count == 0)
                    {
                        // Frame finished — propagate result to parent and pop
                        callStack.Pop();
                        ReturnScope(frame.Scope);

                        if (frame.ReturnField != null)
                        {
                            if (frame.ParentEntity != null)
                                frame.ParentEntity.Fields[frame.ReturnField] = MorphynValue.FromObject(lastAssigned);
                            else if (frame.ParentScope != null)
                                frame.ParentScope[frame.ReturnField] = lastAssigned;
                        }
                        continue;
                    }

                    var item = frame.ActionQueue.Dequeue();
                    bool keepGoing = DispatchSyncAction(data, frame, item, callStack, ref lastAssigned);

                    if (!keepGoing)
                    {
                        // Early exit from check — drain remaining actions in this frame
                        frame.ActionQueue.Clear();
                    }
                }
            }
            finally
            {
                while (callStack.Count > 0)
                    ReturnScope(callStack.Pop().Scope);

                _inSyncContext = wasInSyncContext;

                if (!wasInSyncContext)
                {
                    while (_syncSideEffectQueue.Count > 0)
                        _eventQueue.Enqueue(_syncSideEffectQueue.Dequeue());
                }
            }

            return lastAssigned;
        }

        // Dispatches a single ActionItem within the sync execution loop.
        // Returns false only when a check condition fails with no inline action (stop current frame).
        // Pushing a new SyncFrame onto callStack suspends the current frame until the child finishes.
        private static bool DispatchSyncAction(EntityData data, SyncFrame frame, ActionItem item,
            Stack<SyncFrame> callStack, ref object? lastAssigned)
        {
            var entity = item.Entity;
            var scope = item.Scope;

            switch (item.Action)
            {
                case SetAction set:
                {
                    if (entity.Fields.ContainsKey(set.TargetField))
                    {
                        var mv = EvaluateToValue(entity, set.Expression, scope, data);
                        entity.Fields[set.TargetField] = mv;
                        lastAssigned = mv.ToObject();
                    }
                    else
                    {
                        object? value = EvaluateExpression(entity, set.Expression, scope, data);
                        scope[set.TargetField] = value;
                        lastAssigned = value;
                    }
                    return true;
                }

                case CheckAction check:
                {
                    bool passed = Convert.ToBoolean(EvaluateExpression(entity, check.Condition, scope, data));

                    if (!passed)
                        return check.InlineAction != null; // false = stop frame; true = continue (no-op)

                    if (check.InlineAction != null)
                    {
                        // Inline action executes immediately — flatten and prepend to front of queue
                        var tmp = new Queue<ActionItem>();
                        if (check.InlineAction is BlockAction inlineBlock)
                            EnqueueActions(tmp, inlineBlock.Actions, entity, scope);
                        else
                            tmp.Enqueue(new ActionItem { Action = check.InlineAction, Entity = entity, Scope = scope });

                        // Prepend: drain remaining frame queue after tmp
                        while (frame.ActionQueue.Count > 0)
                            tmp.Enqueue(frame.ActionQueue.Dequeue());
                        while (tmp.Count > 0)
                            frame.ActionQueue.Enqueue(tmp.Dequeue());
                    }
                    return true;
                }

                case SetIndexAction setIdx:
                {
                    object? newValue = EvaluateExpression(entity, setIdx.ValueExpr, scope, data);
                    int index = Convert.ToInt32(EvaluateExpression(entity, setIdx.IndexExpr, scope, data)) - 1;

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fv) && fv.ObjVal is MorphynPool pool)
                    {
                        if (index >= 0 && index < pool.Values.Count)
                            pool.Values[index] = newValue;
                        else
                            throw new Exception($"Index {index + 1} out of bounds for pool '{setIdx.TargetPoolName}'");
                    }
                    else
                        throw new Exception($"Target '{setIdx.TargetPoolName}' is not a pool or not found.");

                    lastAssigned = newValue;
                    return true;
                }

                case EmitWithReturnAction emitRet:
                {
                    var target = ResolveTarget(data, entity, emitRet.TargetEntityName);

                    if (!target.EventCache.TryGetValue(emitRet.EventName, out var nextEv))
                        throw new Exception($"[Sync Error] Event '{emitRet.EventName}' not found in '{target.Name}'.");

                    var resolvedArgs = RentArgsArray(emitRet.Arguments.Count);
                    for (int i = 0; i < emitRet.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRet.Arguments[i], scope, data);

                    var nextScope = RentScope(nextEv.Parameters.Count);
                    for (int k = 0; k < nextEv.Parameters.Count; k++)
                        nextScope[nextEv.Parameters[k]] = k < resolvedArgs.Length ? resolvedArgs[k] : null;
                    ReturnArgsArray(resolvedArgs);

                    bool isEntityField = entity.Fields.ContainsKey(emitRet.TargetField);

                    var nextQueue = new Queue<ActionItem>();
                    EnqueueActions(nextQueue, nextEv.Actions, target, nextScope);

                    callStack.Push(new SyncFrame
                    {
                        Entity = target,
                        Event = nextEv,
                        Scope = nextScope,
                        ActionQueue = nextQueue,
                        ReturnField = emitRet.TargetField,
                        ParentScope = isEntityField ? null : scope,
                        ParentEntity = isEntityField ? entity : null
                    });
                    return true;
                }

                case EmitWithReturnIndexAction emitRetIdx:
                {
                    var target = ResolveTarget(data, entity, emitRetIdx.TargetEntityName);

                    if (!target.EventCache.TryGetValue(emitRetIdx.EventName, out var nextEv))
                        throw new Exception($"[Sync Error] Event '{emitRetIdx.EventName}' not found in '{target.Name}'.");

                    var resolvedArgs = RentArgsArray(emitRetIdx.Arguments.Count);
                    for (int i = 0; i < emitRetIdx.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRetIdx.Arguments[i], scope, data);

                    var nextScope = RentScope(nextEv.Parameters.Count);
                    for (int k = 0; k < nextEv.Parameters.Count; k++)
                        nextScope[nextEv.Parameters[k]] = k < resolvedArgs.Length ? resolvedArgs[k] : null;
                    ReturnArgsArray(resolvedArgs);

                    // Capture index/pool info for the post-call write-back action
                    var capturedIdxExpr = emitRetIdx.IndexExpr;
                    var capturedPoolName = emitRetIdx.TargetPoolName;
                    var capturedEntity = entity;
                    var capturedScope = scope;

                    var nextQueue = new Queue<ActionItem>();
                    EnqueueActions(nextQueue, nextEv.Actions, target, nextScope);

                    // After the child frame finishes and pops, lastAssigned will hold the result.
                    // We inject a write-back action into the *current* frame so it runs next.
                    frame.ActionQueue.Enqueue(new ActionItem
                    {
                        Action = new _PoolIndexWriteAction
                        {
                            IndexExpr = capturedIdxExpr,
                            TargetPoolName = capturedPoolName,
                            CapturedEntity = capturedEntity,
                            CapturedScope = capturedScope
                        },
                        Entity = entity,
                        Scope = scope
                    });

                    callStack.Push(new SyncFrame
                    {
                        Entity = target,
                        Event = nextEv,
                        Scope = nextScope,
                        ActionQueue = nextQueue,
                        ReturnField = null,
                        ParentScope = null,
                        ParentEntity = null
                    });
                    return true;
                }

                case _PoolIndexWriteAction poolWrite:
                {
                    int poolIndex = Convert.ToInt32(EvaluateExpression(
                        poolWrite.CapturedEntity, poolWrite.IndexExpr, poolWrite.CapturedScope, data)) - 1;

                    if (poolWrite.CapturedEntity.Fields.TryGetValue(poolWrite.TargetPoolName, out var pv)
                        && pv.ObjVal is MorphynPool pool)
                    {
                        if (poolIndex >= 0 && poolIndex < pool.Values.Count)
                            pool.Values[poolIndex] = lastAssigned;
                        else
                            throw new Exception($"Index {poolIndex + 1} out of bounds for pool '{poolWrite.TargetPoolName}'");
                    }
                    else
                        throw new Exception($"Target '{poolWrite.TargetPoolName}' is not a pool or not found.");

                    return true;
                }

                case EmitAction emit:
                {
                    var resolvedArgs = RentArgsArray(emit.Arguments.Count);
                    for (int i = 0; i < emit.Arguments.Count; i++)
                    {
                        var argExpr = emit.Arguments[i];
                        try { resolvedArgs[i] = EvaluateExpression(entity, argExpr, scope, data); }
                        catch (Exception) when (emit.EventName == "each" && argExpr is VariableExpression ve)
                        { resolvedArgs[i] = ve.Name; }
                    }

                    if (!HandleBuiltinEmit(data, entity, emit, resolvedArgs, scope))
                        HandleEmitRouting(data, entity, emit, resolvedArgs);

                    ReturnArgsArray(resolvedArgs);
                    return true;
                }

                case WhenAction whenAct:
                {
                    if (data.Entities.TryGetValue(whenAct.TargetEntityName, out var te))
                        Subscribe(entity, te, whenAct.TargetEventName, whenAct.HandlerEventName, whenAct.HandlerArgs);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                    return true;
                }

                case UnwhenAction unwhenAct:
                {
                    if (data.Entities.TryGetValue(unwhenAct.TargetEntityName, out var te))
                        Unsubscribe(entity, te, unwhenAct.TargetEventName, unwhenAct.HandlerEventName);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{unwhenAct.TargetEntityName}' not found.");
                    return true;
                }

                default:
                    return true;
            }
        }

        // Internal trampoline action used to write EmitWithReturnIndex results back into a pool slot.
        // Injected into the parent frame's queue immediately after the child frame is pushed.
        private sealed class _PoolIndexWriteAction : MorphynAction
        {
            public MorphynExpression IndexExpr = null!;
            public string TargetPoolName = null!;
            public Entity CapturedEntity = null!;
            public Dictionary<string, object?> CapturedScope = null!;
        }

        public static void Subscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent, List<MorphynExpression>? handlerArgs = null)
        {
            if (subscriber == target)
            {
                Console.WriteLine($"[Subscription Error] Entity '{subscriber.Name}' cannot subscribe to its own events.");
                return;
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
                if (data.Entities.TryGetValue(parts[0], out var extEntity) &&
                    extEntity.Fields.TryGetValue(parts[1], out var pVal) && pVal.ObjVal is MorphynPool extPool)
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
                            subArgs = RentArgsArray(emit.Arguments.Count - 1);
                            Array.Copy(resolvedArgs, 1, subArgs, 0, emit.Arguments.Count - 1);
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
                    bool passed = Convert.ToBoolean(EvaluateExpression(entity, check.Condition, localScope, data));
                    if (!passed) return check.InlineAction != null;
                    if (check.InlineAction != null) return ExecuteAction(data, entity, check.InlineAction, localScope);
                    return true;
                }

                case SetIndexAction setIdx:
                {
                    object? newValue = EvaluateExpression(entity, setIdx.ValueExpr, localScope, data);
                    int index = Convert.ToInt32(EvaluateExpression(entity, setIdx.IndexExpr, localScope, data)) - 1;

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fv) && fv.ObjVal is MorphynPool pool)
                    {
                        if (index >= 0 && index < pool.Values.Count) pool.Values[index] = newValue;
                        else throw new Exception($"Index {index + 1} out of bounds for pool '{setIdx.TargetPoolName}'");
                    }
                    else throw new Exception($"Target '{setIdx.TargetPoolName}' is not a pool or not found.");
                    return true;
                }

                case BlockAction block:
                    foreach (var sub in block.Actions)
                        if (!ExecuteAction(data, entity, sub, localScope)) return false;
                    return true;

                case EmitWithReturnIndexAction emitRetIdx:
                {
                    var target = ResolveTarget(data, entity, emitRetIdx.TargetEntityName);
                    var resolvedArgs = RentArgsArray(emitRetIdx.Arguments.Count);
                    for (int i = 0; i < emitRetIdx.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRetIdx.Arguments[i], localScope, data);

                    object? syncResult = ExecuteSync(entity, target, emitRetIdx.EventName, resolvedArgs, data);
                    ReturnArgsArray(resolvedArgs);

                    int poolIndex = Convert.ToInt32(EvaluateExpression(entity, emitRetIdx.IndexExpr, localScope, data)) - 1;
                    if (entity.Fields.TryGetValue(emitRetIdx.TargetPoolName, out var pv) && pv.ObjVal is MorphynPool pool)
                    {
                        if (poolIndex >= 0 && poolIndex < pool.Values.Count) pool.Values[poolIndex] = syncResult;
                        else throw new Exception($"Index {poolIndex + 1} out of bounds for pool '{emitRetIdx.TargetPoolName}'");
                    }
                    else throw new Exception($"Target '{emitRetIdx.TargetPoolName}' is not a pool or not found.");
                    return true;
                }

                case EmitWithReturnAction emitRet:
                {
                    var target = ResolveTarget(data, entity, emitRet.TargetEntityName);
                    var resolvedArgs = RentArgsArray(emitRet.Arguments.Count);
                    for (int i = 0; i < emitRet.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateExpression(entity, emitRet.Arguments[i], localScope, data);

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
                    var resolvedArgs = RentArgsArray(emit.Arguments.Count);
                    for (int i = 0; i < emit.Arguments.Count; i++)
                    {
                        var argExpr = emit.Arguments[i];
                        try { resolvedArgs[i] = EvaluateExpression(entity, argExpr, localScope, data); }
                        catch (Exception) when (emit.EventName == "each" && argExpr is VariableExpression ve)
                        { resolvedArgs[i] = ve.Name; }
                    }
                    if (!HandleBuiltinEmit(data, entity, emit, resolvedArgs, localScope))
                        HandleEmitRouting(data, entity, emit, resolvedArgs);
                    ReturnArgsArray(resolvedArgs);
                    return true;
                }

                case WhenAction whenAct:
                    if (data.Entities.TryGetValue(whenAct.TargetEntityName, out var wte))
                        Subscribe(entity, wte, whenAct.TargetEventName, whenAct.HandlerEventName, whenAct.HandlerArgs);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                    return true;

                case UnwhenAction unwhenAct:
                    if (data.Entities.TryGetValue(unwhenAct.TargetEntityName, out var ute))
                        Unsubscribe(entity, ute, unwhenAct.TargetEventName, unwhenAct.HandlerEventName);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{unwhenAct.TargetEntityName}' not found.");
                    return true;

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
                    if (args[0] == null) throw new Exception("Insert index cannot be null");
                    pool.Values.Insert(Convert.ToInt32(args[0]) - 1, args[1]);
                    return true;
                case "remove_at":
                    if (args[0] == null) throw new Exception("Remove_at index cannot be null");
                    int idxRem = Convert.ToInt32(args[0]) - 1;
                    if (idxRem >= 0 && idxRem < pool.Values.Count) pool.Values.RemoveAt(idxRem);
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
                    if (args[0] == null || args[1] == null) throw new Exception("Swap indices cannot be null");
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
                foreach (var field in e.Fields.Values)
                    if (field.ObjVal is MorphynPool pool)
                        pool.Values.RemoveAll(item => item is Entity { IsDestroyed: true });

            foreach (var list in _subscriptions.Values)
                list.RemoveAll(s => s.subscriber.IsDestroyed);

            _needsCleanup = false;
        }
    }
}