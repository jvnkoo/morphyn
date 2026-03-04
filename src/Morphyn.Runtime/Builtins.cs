using System;
using System.Collections.Generic;
using System.Linq;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    internal static class Builtins
    {
        public static bool HandleBuiltinEmit(EntityData data, Entity entity, EmitAction emit,
            MorphynValue[] resolvedArgs, Dictionary<string, MorphynValue> localScope)
        {
            if (emit.TargetEntityName == "self" && emit.EventName == "destroy")
            {
                entity.IsDestroyed = true;
                MorphynRuntime.MarkDirty();
                return true;
            }

            if (emit.EventName == "log")
            {
                for (int i = 0; i < emit.Arguments.Count; i++)
                {
                    var arg = resolvedArgs[i].ToObject();
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
                string prompt = emit.Arguments.Count > 0 ? resolvedArgs[0].ToObject()?.ToString() ?? "" : "";
                Console.Write(prompt);
                string? line = Console.ReadLine();

                string targetField = emit.Arguments.Count > 1 ? resolvedArgs[1].ToObject()?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(targetField))
                {
                    if (double.TryParse(line, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double num))
                    {
                        if (entity.Fields.ContainsKey(targetField)) entity.Fields[targetField] = MorphynValue.FromDouble(num);
                        else localScope[targetField] = MorphynValue.FromDouble(num);
                    }
                    else
                    {
                        if (entity.Fields.ContainsKey(targetField)) entity.Fields[targetField] = MorphynValue.FromObject(line);
                        else localScope[targetField] = MorphynValue.FromObject(line);
                    }
                }
                return true;
            }

            if (emit.EventName == "unity")
            {
                if (MorphynRuntime.UnityCallback != null && emit.Arguments.Count > 0)
                {
                    string callbackName = resolvedArgs[0].ToObject()?.ToString() ?? "";
                    object?[] callbackArgs = ObjectPools.Empty.Select(v => v.ToObject()).ToArray();
                    if (emit.Arguments.Count > 1)
                    {
                        callbackArgs = new object?[emit.Arguments.Count - 1];
                        for (int i = 1; i < emit.Arguments.Count; i++)
                            callbackArgs[i - 1] = resolvedArgs[i].ToObject();
                    }
                    MorphynRuntime.UnityCallback(callbackName, callbackArgs);
                }
                return true;
            }

            return false;
        }
    }
}