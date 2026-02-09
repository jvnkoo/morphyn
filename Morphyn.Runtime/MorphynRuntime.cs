/**
 * \file MorphynRuntime.cs
 * \brief Morphyn Runtime System
 * \defgroup runtime Runtime System
 * @{
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Morphyn.Parser;
using static Morphyn.Runtime.MorphynEvaluator;

namespace Morphyn.Runtime
{
    /**
     * \class MorphynRuntime
     * \brief Event processing and entity lifecycle management
     *
     * \page event_system Event System
     *
     * \section event_overview Overview
     *
     * Morphyn uses an event queue to process entity reactions. Events are processed
     * in order, and each event can trigger additional events.
     *
     * \section builtin_events Built-in Events
     *
     * \subsection init_event init
     *
     * Called when an entity is first created or spawned:
     *
     * \code{.morphyn}
     * entity Enemy {
     *   has hp: 50
     *
     *   on init {
     *     emit log("Enemy spawned")
     *   }
     * }
     * \endcode
     *
     * \subsection tick_event tick(dt)
     *
     * Called every frame with delta time in milliseconds:
     *
     * \code{.morphyn}
     * entity Timer {
     *   has time: 0
     *
     *   on tick(dt) {
     *     time + dt -> time
     *   }
     * }
     * \endcode
     *
     * \subsection destroy_event destroy
     *
     * Marks entity for garbage collection:
     *
     * \code{.morphyn}
     * entity Enemy {
     *   has hp: 50
     *
     *   on damage(v) {
     *     hp - v -> hp
     *     check hp <= 0: emit self.destroy
     *   }
     * }
     * \endcode
     *
     * \section custom_events Custom Events
     *
     * Define your own events:
     *
     * \code{.morphyn}
     * entity Player {
     *   on jump {
     *     emit log("Player jumped!")
     *   }
     *
     *   on heal(amount) {
     *     hp + amount -> hp
     *   }
     * }
     * \endcode
     */
    public static class MorphynRuntime
    {
        private static readonly Queue<PendingEvent> _eventQueue = new();
        private static readonly List<object?> EmptyArgs = new List<object?>(0);

        public static Action<string, object?[]>? UnityCallback { get; set; }

        /**
         * \brief Send an event to an entity
         * \param target The entity that will receive the event
         * \param eventName Name of the event to send
         * \param args Optional arguments to pass to the event handler
         *
         * \par Example (Morphyn):
         * \code{.morphyn}
         * emit target.damage(10)
         * emit self.destroy
         * emit log("message")
         * \endcode
         */
        public static void Send(Entity target, string eventName, List<object?>? args = null)
        {
            if (_eventQueue.Any(e => e.EventName == eventName && e.Target == target))
                return;
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

            var localScope = new Dictionary<string, object?>();
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

                case EmitAction emit:
                {
                    List<object?> resolvedArgs = new List<object?>();

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

                    string? targetName = emit.TargetEntityName;
                    string poolName = emit.EventName;

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

                    if (!string.IsNullOrEmpty(emit.TargetEntityName))
                    {
                        if (entity.Fields.TryGetValue(emit.TargetEntityName, out var poolObj) &&
                            poolObj is MorphynPool localPool)
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

                    Entity? target = string.IsNullOrEmpty(targetName)
                        ? entity
                        : (data.Entities.TryGetValue(targetName, out var e) ? e : null);

                    if (target != null)
                    {
                        Send(target, emit.EventName, resolvedArgs);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"[ERROR] Target entity '{targetName}' not found for event '{emit.EventName}'");
                    }

                    return true;
                }

                default:
                    return true;
            }
        }

        /**
         * \page pools Pool System
         *
         * \section pool_overview Overview
         *
         * Pools are collections of entities or values in Morphyn. They provide
         * high-performance storage for game objects.
         *
         * \section pool_declaration Declaration
         *
         * \code{.morphyn}
         * entity World {
         *   has enemies: pool[1, 2, 3]
         *   has items: pool["sword", "shield"]
         *   has positions: pool[0.0, 10.5, 20.3]
         * }
         * \endcode
         *
         * \section pool_commands Pool Commands
         *
         * \subsection pool_add Adding Elements
         *
         * \par add - Add entity instance
         * \code{.morphyn}
         * emit enemies.add(Enemy)  # Creates new Enemy and adds to pool
         * \endcode
         *
         * \par push - Add to front
         * \code{.morphyn}
         * emit items.push("new_item")
         * \endcode
         *
         * \par insert - Insert at position (1-based index)
         * \code{.morphyn}
         * emit items.insert(2, "middle_item")
         * \endcode
         *
         * \subsection pool_remove Removing Elements
         *
         * \par remove - Remove specific value
         * \code{.morphyn}
         * emit enemies.remove(target)
         * \endcode
         *
         * \par remove_at - Remove at index (1-based)
         * \code{.morphyn}
         * emit enemies.remove_at(3)
         * \endcode
         *
         * \par pop - Remove last element
         * \code{.morphyn}
         * emit items.pop
         * \endcode
         *
         * \par shift - Remove first element
         * \code{.morphyn}
         * emit items.shift
         * \endcode
         *
         * \par clear - Remove all elements
         * \code{.morphyn}
         * emit enemies.clear
         * \endcode
         *
         * \subsection pool_other Other Operations
         *
         * \par swap - Swap two elements (1-based indices)
         * \code{.morphyn}
         * emit items.swap(1, 3)
         * \endcode
         *
         * \par each - Call event on each element
         * \code{.morphyn}
         * emit enemies.each(update, dt)
         * emit items.each(collect, player)
         * \endcode
         *
         * \section pool_access Accessing Pools
         *
         * \par Get pool size
         * \code{.morphyn}
         * enemies.count -> num_enemies
         * \endcode
         *
         * \par Access by index (1-based)
         * \code{.morphyn}
         * enemies.at[1] -> first_enemy
         * enemies.at[i] -> current_enemy
         * \endcode
         *
         * \par Set by index
         * \code{.morphyn}
         * new_value -> pool.at[index]
         * \endcode
         */
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
        }
    }
}
/** @} */ // end of runtime group2