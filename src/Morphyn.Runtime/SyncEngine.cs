using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphyn.Parser;
using static Morphyn.Runtime.MorphynEvaluator;

namespace Morphyn.Runtime
{
    // A pending action in the iterative sync execution engine.
    // When ReturnTarget is set and the action is an EmitWithReturn, the completed child frame
    // writes its result back into ParentScope[ReturnTarget] before the parent resumes.
    internal struct ActionItem
    {
        public MorphynAction Action;
        public Entity Entity;
        public Dictionary<string, MorphynValue> Scope;
        // Non-null only for EmitWithReturnAction: field to write result into when child completes
        public string? ReturnField;
        // True = write result into entity.Fields[ReturnField], false = write into Scope[ReturnField]
        public bool ReturnToEntityField;
    }

    // Each SyncFrame represents one active event invocation on the explicit call stack.
    // ActionQueue contains the remaining actions to execute for this frame.
    // When the queue empties the frame is popped and the result propagates to the parent.
    internal class SyncFrame
    {
        public Entity Entity = null!;
        public Event Event = null!;
        public Dictionary<string, MorphynValue> Scope = null!;
        public Queue<ActionItem> ActionQueue = null!;
        // Where to write lastAssigned when this frame finishes (into parent scope or entity field)
        public string? ReturnField;
        public Dictionary<string, MorphynValue>? ParentScope;
        public Entity? ParentEntity;
    }

    // Internal trampoline action used to write EmitWithReturnIndex results back into a pool slot.
    // Injected into the parent frame's queue immediately after the child frame is pushed.
    internal sealed class _PoolIndexWriteAction : MorphynAction
    {
        public _PoolIndexWriteAction() => Kind = ActionKind.PoolIndexWrite;
        public MorphynExpression IndexExpr = null!;
        public string TargetPoolName = null!;
        public Entity CapturedEntity = null!;
        public Dictionary<string, MorphynValue> CapturedScope = null!;
    }

    internal static class SyncEngine
    {
        // Resolves "self", empty string, and named entities uniformly.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Entity ResolveTarget(EntityData data, Entity current, string? name)
        {
            if (string.IsNullOrEmpty(name) || name == "self") return current;
            if (data.Entities.TryGetValue(name, out var e)) return e;
            throw new Exception($"[Sync Error] Entity '{name}' not found.");
        }

        // Flattens an action list into an ActionItem queue, inlining BlockAction children
        // so the main loop never needs to recurse into blocks.
        public static void EnqueueActions(Queue<ActionItem> queue, MorphynAction[] actions,
            Entity entity, Dictionary<string, MorphynValue> scope)
        {
            for (int i = 0; i < actions.Length; i++)
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
            string eventName, MorphynValue[] args, EntityData data,
            bool wasInSyncContext, Queue<PendingEvent> syncSideEffectQueue, Queue<PendingEvent> eventQueue)
        {
            if (!targetEntity.EventCache.TryGetValue(eventName, out var firstEv))
                throw new Exception($"[Sync Error] Event '{eventName}' not found in '{targetEntity.Name}'.");

            object? lastAssigned = null;

            var callStack = new Stack<SyncFrame>();

            var firstScope = ObjectPools.RentScope(firstEv.Parameters.Count);
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
                        ObjectPools.ReturnScope(frame.Scope);

                        if (frame.ReturnField != null)
                        {
                            if (frame.ParentEntity != null)
                            {
                                var newMv = MorphynValue.FromObject(lastAssigned);
                                frame.ParentEntity.Fields.TryGetValue(frame.ReturnField, out var oldMv);
                                frame.ParentEntity.Fields[frame.ReturnField] = newMv;
                                Subscriptions.NotifyFieldChanged(frame.ParentEntity, frame.ReturnField, oldMv, newMv);
                            }
                            else if (frame.ParentScope != null)
                                frame.ParentScope[frame.ReturnField] = MorphynValue.FromObject(lastAssigned);
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
                    ObjectPools.ReturnScope(callStack.Pop().Scope);

                if (!wasInSyncContext)
                {
                    while (syncSideEffectQueue.Count > 0)
                        eventQueue.Enqueue(syncSideEffectQueue.Dequeue());
                }
            }

            return lastAssigned;
        }

        // Dispatches a single ActionItem within the sync execution loop.
        // Returns false only when a check condition fails with no inline action (stop current frame).
        // Pushing a new SyncFrame onto callStack suspends the current frame until the child finishes.
        public static bool DispatchSyncAction(EntityData data, SyncFrame frame, ActionItem item,
            Stack<SyncFrame> callStack, ref object? lastAssigned)
        {
            var entity = item.Entity;
            var scope = item.Scope;

            switch (item.Action.Kind)
            {
                case ActionKind.Set:
                {
                        var set = Unsafe.As<SetAction>(item.Action);
                        if (entity.Fields.ContainsKey(set.TargetField))
                        {
                            var mv = EvaluateToValue(entity, set.Expression, scope, data);
                            entity.Fields.TryGetValue(set.TargetField, out var oldMv);
                            entity.Fields[set.TargetField] = mv;
                            Subscriptions.NotifyFieldChanged(entity, set.TargetField, oldMv, mv);
                            lastAssigned = mv.ToObject();
                        }
                        else
                        {
                            var mv = EvaluateToValue(entity, set.Expression, scope, data);
                            scope[set.TargetField] = mv;
                            lastAssigned = mv.ToObject();
                        }
                        return true;
                    }

                case ActionKind.Check:
                {
                    var check = Unsafe.As<CheckAction>(item.Action);
                    var condVal = EvaluateToValue(entity, check.Condition, scope, data);
                    bool passed = condVal.Kind switch
                    {
                        MorphynValueKind.Bool   => condVal.BoolVal,
                        MorphynValueKind.Double => condVal.NumVal != 0,
                        MorphynValueKind.Null   => false,
                        _                       => Convert.ToBoolean(condVal.ToObject())
                    };

                    if (!passed)
                        return check.InlineAction != null; // false = stop frame; true = continue (no-op)

                    if (check.InlineAction != null)
                    {
                        // Inline action executes immediately — flatten and prepend to front of queue
                        var tmp = new Queue<ActionItem>();
                        if (check.InlineAction.Kind == ActionKind.Block)
                            EnqueueActions(tmp, Unsafe.As<BlockAction>(check.InlineAction).Actions, entity, scope);
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

                case ActionKind.SetIndex:
                {
                    var setIdx = Unsafe.As<SetIndexAction>(item.Action);
                    var newValue = EvaluateToValue(entity, setIdx.ValueExpr, scope, data);
                    int index = (int)EvaluateToValue(entity, setIdx.IndexExpr, scope, data).NumVal - 1;

                    if (entity.Fields.TryGetValue(setIdx.TargetPoolName, out var fv) && fv.ObjVal is MorphynPool pool)
                    {
                        if (index >= 0 && index < pool.Values.Count)
                            pool.Values[index] = newValue;
                        else
                            throw new Exception($"Index {index + 1} out of bounds for pool '{setIdx.TargetPoolName}'");
                    }
                    else
                        throw new Exception($"Target '{setIdx.TargetPoolName}' is not a pool or not found.");

                    lastAssigned = newValue.ToObject();
                    return true;
                }

                case ActionKind.EmitWithReturn:
                {
                    var emitRet = Unsafe.As<EmitWithReturnAction>(item.Action);
                    var target = ResolveTarget(data, entity, emitRet.TargetEntityName);

                    if (!target.EventCache.TryGetValue(emitRet.EventName, out var nextEv))
                        throw new Exception($"[Sync Error] Event '{emitRet.EventName}' not found in '{target.Name}'.");

                    var resolvedArgs = ObjectPools.RentArgsArray(emitRet.Arguments.Count);
                    for (int i = 0; i < emitRet.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateToValue(entity, emitRet.Arguments[i], scope, data);

                    var nextScope = ObjectPools.RentScope(nextEv.Parameters.Count);
                    for (int k = 0; k < nextEv.Parameters.Count; k++)
                        nextScope[nextEv.Parameters[k]] = k < resolvedArgs.Length ? resolvedArgs[k] : MorphynValue.Null;
                    ObjectPools.ReturnArgsArray(resolvedArgs);

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

                case ActionKind.EmitWithReturnIndex:
                {
                    var emitRetIdx = Unsafe.As<EmitWithReturnIndexAction>(item.Action);
                    var target = ResolveTarget(data, entity, emitRetIdx.TargetEntityName);

                    if (!target.EventCache.TryGetValue(emitRetIdx.EventName, out var nextEv))
                        throw new Exception($"[Sync Error] Event '{emitRetIdx.EventName}' not found in '{target.Name}'.");

                    var resolvedArgs = ObjectPools.RentArgsArray(emitRetIdx.Arguments.Count);
                    for (int i = 0; i < emitRetIdx.Arguments.Count; i++)
                        resolvedArgs[i] = EvaluateToValue(entity, emitRetIdx.Arguments[i], scope, data);

                    var nextScope = ObjectPools.RentScope(nextEv.Parameters.Count);
                    for (int k = 0; k < nextEv.Parameters.Count; k++)
                        nextScope[nextEv.Parameters[k]] = k < resolvedArgs.Length ? resolvedArgs[k] : MorphynValue.Null;
                    ObjectPools.ReturnArgsArray(resolvedArgs);

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

                case ActionKind.PoolIndexWrite:
                {
                    var poolWrite = Unsafe.As<_PoolIndexWriteAction>(item.Action);
                    int poolIndex = (int)EvaluateToValue(
                        poolWrite.CapturedEntity, poolWrite.IndexExpr, poolWrite.CapturedScope, data).NumVal - 1;

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

                case ActionKind.Emit:
                {
                    var emit = Unsafe.As<EmitAction>(item.Action);
                    var resolvedArgs = ObjectPools.RentArgsArray(emit.Arguments.Count);
                    for (int i = 0; i < emit.Arguments.Count; i++)
                    {
                        var argExpr = emit.Arguments[i];
                        try { resolvedArgs[i] = EvaluateToValue(entity, argExpr, scope, data); }
                        catch (Exception) when (emit.EventName == "each" && argExpr.Kind == ExprKind.Variable)
                        { resolvedArgs[i] = MorphynValue.FromObject(Unsafe.As<VariableExpression>(argExpr).Name); }
                    }

                    if (!Builtins.HandleBuiltinEmit(data, entity, emit, resolvedArgs, scope))
                        MorphynRuntime.HandleEmitRouting(data, entity, emit, resolvedArgs);

                    ObjectPools.ReturnArgsArray(resolvedArgs);
                    return true;
                }

                case ActionKind.When:
                {
                    var whenAct = Unsafe.As<WhenAction>(item.Action);
                    if (data.Entities.TryGetValue(whenAct.TargetEntityName, out var te))
                        Subscriptions.Subscribe(entity, te, whenAct.TargetEventName, whenAct.HandlerEventName, whenAct.HandlerArgs);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{whenAct.TargetEntityName}' not found.");
                    return true;
                }

                case ActionKind.Unwhen:
                {
                    var unwhenAct = Unsafe.As<UnwhenAction>(item.Action);
                    if (data.Entities.TryGetValue(unwhenAct.TargetEntityName, out var te))
                        Subscriptions.Unsubscribe(entity, te, unwhenAct.TargetEventName, unwhenAct.HandlerEventName);
                    else
                        Console.WriteLine($"[Subscription Error] Entity '{unwhenAct.TargetEntityName}' not found.");
                    return true;
                }

                default:
                    return true;
            }
        }
    }
}