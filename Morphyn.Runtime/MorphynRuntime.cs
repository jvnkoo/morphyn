using System;
using System.Linq;
using Morphyn.Parser;
using static Morphyn.Runtime.MorphynEvaluator;

namespace Morphyn.Runtime
{
    public static class MorphynRuntime
    {
        public static void Emit(EntityData data, Entity entity, string eventName)
        {
            var ev = entity.Events.FirstOrDefault(e => e.Name == eventName);
            if (ev == null) return;

            Console.WriteLine($"\n[Runtime] >>> Triggering '{eventName}' on {entity.Name}");

            foreach (var action in ev.Actions)
            {
                if (!ExecuteAction(data, entity, action))
                {
                    Console.WriteLine($"[Runtime] Event '{eventName}' halted due to failed check.");
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

                    if (!string.IsNullOrEmpty(emit.TargetEntityName))
                    {
                        if (data.Entities.TryGetValue(emit.TargetEntityName, out var targetEntity))
                        {
                            Emit(data, targetEntity, emit.EventName);
                        }
                        else
                        {
                            Console.WriteLine($"[Runtime Error] Entity '{emit.TargetEntityName}' not found.");
                        }
                    }
                    else
                    {
                        Emit(data, entity, emit.EventName);
                    }
                    return true;
                case CheckAction check:
                    return EvaluateCheck(data, entity, check.Expression);
                
                default:
                    return true;
            }
        }
    }
}