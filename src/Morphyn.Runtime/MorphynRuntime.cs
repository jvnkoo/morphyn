using System;
using System.Collections.Generic;
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
        private static readonly HashSet<(Entity, string)> _pendingEventSet = new();
        private const int HASH_SET_THRESHOLD = 20;

        private static bool _needsCleanup = false;

        // Side-effect emits fired inside sync events go here, drained immediately after sync completes
        private static readonly Queue<PendingEvent> _syncSideEffectQueue = new();
        private static bool _inSyncContext = false;

        public static Action<string, object?[]>? UnityCallback { get; set; }

        public static Action<string, string, object?[]>? OnEventFired { get; set; }

        public static void MarkDirty() => _needsCleanup = true;

        public static void Send(Entity entity, string eventName, params MorphynValue[] args)
        {
            int argCount = args?.Length ?? 0;

            if (_eventQueue.Count > 0)
            {
                foreach (var e in _eventQueue)
                {
                    if (e.Target == entity && e.EventName == eventName && ArgsEqual(e.Args, args))
                        return;
                }
            }

            MorphynValue[] eventArgs = ObjectPools.RentArgsArray(argCount);
            if (args != null) Array.Copy(args, eventArgs, argCount);

            var pendingEvent = new PendingEvent(entity, eventName, eventArgs, argCount);

            if (_inSyncContext)
                _syncSideEffectQueue.Enqueue(pendingEvent);
            else
                _eventQueue.Enqueue(pendingEvent);

            // OnEventFired?.Invoke(entity.Name, eventName, args?.Select(a => a.ToObject()).ToArray());

            if (Subscriptions.TryGetSubscribers(entity.Name, eventName, out var subscribers))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var sub = subscribers[i];
                    if (!sub.subscriber.IsDestroyed)
                    {
                        MorphynValue[]? resolvedArgs = null;
                        if (sub.handlerArgs != null)
                        {
                            resolvedArgs = ObjectPools.RentArgsArray(sub.handlerArgs.Count);
                            var emptyScope = ObjectPools.RentScope(0);
                            try
                            {
                                for (int j = 0; j < sub.handlerArgs.Count; j++)
                                {
                                    resolvedArgs[j] = MorphynEvaluator.EvaluateToValue(sub.subscriber, sub.handlerArgs[j], emptyScope, _currentData!);
                                }
                            }
                            finally
                            {
                                ObjectPools.ReturnScope(emptyScope);
                            }
                        }

                        Send(sub.subscriber, sub.handler, resolvedArgs ?? Array.Empty<MorphynValue>());
                        if (resolvedArgs != null) ObjectPools.ReturnArgsArray(resolvedArgs);
                    }
                }
            }
        }

        private static bool ArgsEqual(MorphynValue[] a, MorphynValue[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Kind != b[i].Kind) return false;
                bool equal = a[i].Kind switch
                {
                    MorphynValueKind.Double => a[i].NumVal == b[i].NumVal,
                    MorphynValueKind.Bool => a[i].BoolVal == b[i].BoolVal,
                    MorphynValueKind.String => (string?)a[i].ObjVal == (string?)b[i].ObjVal,
                    _ => ReferenceEquals(a[i].ObjVal, b[i].ObjVal)
                };
                if (!equal) return false;
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
                ObjectPools.ReturnArgsArray(current.Args);
            }
        }

        private static void ProcessEvent(EntityData data, PendingEvent pending)
        {
            var entity = pending.Target;
            if (entity.IsDestroyed || !entity.EventCache.TryGetValue(pending.EventName, out var ev)) return;

            var localScope = ObjectPools.RentScope(ev.Parameters.Count);
            for (int i = 0; i < ev.Parameters.Count; i++)
            {
                localScope[ev.Parameters[i]] = i < pending.ArgCount
                    ? pending.Args[i]
                    : throw new Exception($"Event '{pending.EventName}' expected {ev.Parameters.Count} arguments.");
            }

            try
            {
                var actions = ev.Actions;
                for (int ai = 0; ai < actions.Length; ai++)
                {
                    if (!ExecuteAction(data, entity, actions[ai], localScope))
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Runtime Error] Entity '{entity.Name}', event '{pending.EventName}': {ex.Message}");
            }
            finally
            {
                ObjectPools.ReturnScope(localScope);
            }
        }

        public static object? ExecuteSync(Entity callerEntity, Entity targetEntity,
            string eventName, MorphynValue[] args, EntityData data)
        {
            bool wasInSyncContext = _inSyncContext;
            _inSyncContext = true;

            try
            {
                return SyncEngine.ExecuteSync(
                    callerEntity, targetEntity, eventName, args, data,
                    wasInSyncContext, _syncSideEffectQueue, _eventQueue);
            }
            finally
            {
                _inSyncContext = wasInSyncContext;
            }
        }

        public static void Subscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent, List<MorphynExpression>? handlerArgs = null)
            => Subscriptions.Subscribe(subscriber, target, targetEvent, handlerEvent, handlerArgs);

        public static void Unsubscribe(Entity subscriber, Entity target,
            string targetEvent, string handlerEvent)
            => Subscriptions.Unsubscribe(subscriber, target, targetEvent, handlerEvent);
        // Called by MorphynBuiltins and MorphynSyncEngine
        internal static bool HandleEmitRouting(EntityData data, Entity entity, EmitAction emit,
            MorphynValue[] resolvedArgs)
        {
            string? targetName = emit.TargetEntityName == "self" ? null : emit.TargetEntityName;

            if (!string.IsNullOrEmpty(targetName) && targetName.Contains('.'))
            {
                var parts = targetName.Split('.');
                if (data.Entities.TryGetValue(parts[0], out var extEntity) &&
                    extEntity.Fields.TryGetValue(parts[1], out var pVal) && pVal.ObjVal is MorphynPool extPool)
                {
                    if (PoolCommands.HandlePoolCommand(extPool, emit.EventName, resolvedArgs, data)) return true;
                }
            }

            if (!string.IsNullOrEmpty(targetName))
            {
                if (entity.Fields.TryGetValue(targetName, out var poolVal) && poolVal.ObjVal is MorphynPool localPool)
                {
                    if (emit.EventName == "each")
                    {
                        string subEventName = resolvedArgs[0].ToString() ?? "";
                        MorphynValue[] subArgs = ObjectPools.Empty;
                        if (emit.Arguments.Count > 1)
                        {
                            subArgs = ObjectPools.RentArgsArray(emit.Arguments.Count - 1);
                            Array.Copy(resolvedArgs, 1, subArgs, 0, emit.Arguments.Count - 1);
                        }
                        foreach (var item in localPool.Values)
                        {
                            if (item is Entity subE) Send(subE, subEventName, subArgs);
                            else if (item is string eName && data.Entities.TryGetValue(eName, out var extE))
                                Send(extE, subEventName, subArgs);
                        }
                        if (subArgs != ObjectPools.Empty) ObjectPools.ReturnArgsArray(subArgs);
                        return true;
                    }
                    if (PoolCommands.HandlePoolCommand(localPool, emit.EventName, resolvedArgs, data)) return true;
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
            Dictionary<string, MorphynValue> localScope)
        {
            switch (action.Kind)
            {
                case ActionKind.Set:
                {
                    var set = Unsafe.As<SetAction>(action);
                    if (entity.Fields.ContainsKey(set.TargetField))
                        entity.Fields[set.TargetField] = EvaluateToValue(entity, set.Expression, localScope, data);
                    else
                        localScope[set.TargetField] = EvaluateToValue(entity, set.Expression, localScope, data);
                    return true;
                }

                case ActionKind.Check:
                {
                    var check = Unsafe.As<CheckAction>(action);
                    var condVal = EvaluateToValue(entity, check.Condition, localScope, data);
                    bool passed = condVal.Kind switch
                    {
                        MorphynValueKind.Bool   => condVal.BoolVal,
                        MorphynValueKind.Double => condVal.NumVal != 0,
                        MorphynValueKind.Null   => false,
                        _                       => Convert.ToBoolean(condVal.ToObject())
                    };
                    if (!passed) return check.InlineAction != null;
                    if (check.InlineAction != null) return ExecuteAction(data, entity, check.InlineAction, localScope);
                    return true;
                }

                case ActionKind.SetIndex:
                {
                    var setIdx = Unsafe.As<SetIndexAction>(action);
                    var newValue = EvaluateToValue(entity, setIdx.ValueExpr, localScope, data);
                    int index = (int)EvaluateToValue(entity, setIdx.IndexExpr, localScope, data).NumVal - 1;
                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fv) && fv.ObjVal is MorphynPool pool)
                    {
                        if (index >= 0 && index < pool.Values.Count) pool.Values[index] = newValue;
                        else throw new Exception($"Index {index + 1} out of bounds for pool '{setIdx.TargetPoolName}'");
                    }
                    else throw new Exception($"Target '{setIdx.TargetPoolName}' is not a pool or not found.");
                    return true;
                }

                case ActionKind.Block:
                {
                    var blockActions = Unsafe.As<BlockAction>(action).Actions;
                    for (int bi = 0; bi < blockActions.Length; bi++)
                        if (!ExecuteAction(data, entity, blockActions[bi], localScope)) return false;
                    return true;
                }

                case ActionKind.EmitWithReturnIndex:
                {
                    var emitRetIdx = Unsafe.As<EmitWithReturnIndexAction>(action);
                    var target = SyncEngine.ResolveTarget(data, entity, emitRetIdx.TargetEntityName);
                    var resolvedArgs = ObjectPools.RentArgsArray(emitRetIdx.Arguments.Count);
                    for (int i = 0; i < emitRetIdx.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateToValue(entity, emitRetIdx.Arguments[i], localScope, data);
                    object? syncResult = ExecuteSync(entity, target, emitRetIdx.EventName, resolvedArgs, data);
                    ObjectPools.ReturnArgsArray(resolvedArgs);
                    int poolIndex = (int)EvaluateToValue(entity, emitRetIdx.IndexExpr, localScope, data).NumVal - 1;
                    if (entity.Fields.TryGetValue(emitRetIdx.TargetPoolName, out var pv) && pv.ObjVal is MorphynPool pool2)
                    {
                        if (poolIndex >= 0 && poolIndex < pool2.Values.Count) pool2.Values[poolIndex] = MorphynValue.FromObject(syncResult);
                        else throw new Exception($"Index {poolIndex + 1} out of bounds for pool '{emitRetIdx.TargetPoolName}'");
                    }
                    else throw new Exception($"Target '{emitRetIdx.TargetPoolName}' is not a pool or not found.");
                    return true;
                }

                case ActionKind.EmitWithReturn:
                {
                    var emitRet = Unsafe.As<EmitWithReturnAction>(action);
                    var target = SyncEngine.ResolveTarget(data, entity, emitRet.TargetEntityName);
                    var resolvedArgs = ObjectPools.RentArgsArray(emitRet.Arguments.Count);
                    for (int i = 0; i < emitRet.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateToValue(entity, emitRet.Arguments[i], localScope, data);
                    object? syncResult = ExecuteSync(entity, target, emitRet.EventName, resolvedArgs, data);
                    ObjectPools.ReturnArgsArray(resolvedArgs);
                    if (entity.Fields.ContainsKey(emitRet.TargetField))
                        entity.Fields[emitRet.TargetField] = MorphynValue.FromObject(syncResult);
                    else
                        localScope[emitRet.TargetField] = MorphynValue.FromObject(syncResult);
                    return true;
                }

                case ActionKind.Emit:
                {
                    var emit = Unsafe.As<EmitAction>(action);
                    var resolvedArgs = ObjectPools.RentArgsArray(emit.Arguments.Count);
                    for (int i = 0; i < emit.Arguments.Count; i++)
                    {
                        var argExpr = emit.Arguments[i];
                        try { resolvedArgs[i] = EvaluateToValue(entity, argExpr, localScope, data); }
                        catch (Exception) when (emit.EventName == "each" && argExpr.Kind == ExprKind.Variable)
                        { resolvedArgs[i] = MorphynValue.FromObject(Unsafe.As<VariableExpression>(argExpr).Name); }
                    }
                    if (!Builtins.HandleBuiltinEmit(data, entity, emit, resolvedArgs, localScope))
                    {
                        string? targetName = emit.TargetEntityName == "self" ? null : emit.TargetEntityName;
                        Entity? emitTarget = string.IsNullOrEmpty(targetName)
                            ? entity
                            : (data.Entities.TryGetValue(targetName, out var e) ? e : null);
                        if (emitTarget != null)
                            ExecuteSync(entity, emitTarget, emit.EventName, resolvedArgs, data);
                        else
                            HandleEmitRouting(data, entity, emit, resolvedArgs);
                    }
                    ObjectPools.ReturnArgsArray(resolvedArgs);
                    return true;
                }

                case ActionKind.When:
                {
                    var whenAct = Unsafe.As<WhenAction>(action);
                    if (data.Entities.TryGetValue(whenAct.TargetEntityName, out var wte))
                        Subscriptions.Subscribe(entity, wte, whenAct.TargetEventName, whenAct.HandlerEventName, whenAct.HandlerArgs);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                    return true;
                }

                case ActionKind.Unwhen:
                {
                    var unwhenAct = Unsafe.As<UnwhenAction>(action);
                    if (data.Entities.TryGetValue(unwhenAct.TargetEntityName, out var ute))
                        Subscriptions.Unsubscribe(entity, ute, unwhenAct.TargetEventName, unwhenAct.HandlerEventName);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{unwhenAct.TargetEntityName}' not found.");
                    return true;
                }

                default:
                    return true;
            }
        }

        public static void GarbageCollect(EntityData data)
        {
            if (!_needsCleanup) return;

            foreach (var e in data.Entities.Values)
                foreach (var field in e.Fields.Values)
                    if (field.ObjVal is MorphynPool pool)
                        pool.Values.RemoveAll(item => item is Entity { IsDestroyed: true });

            Subscriptions.RemoveDestroyedSubscribers();

            _needsCleanup = false;
        }
    }
}