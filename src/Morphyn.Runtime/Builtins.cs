using System;
using System.Collections.Generic;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    internal static class Builtins
    {
        public static bool HandleBuiltinEmit(EntityData data, Entity entity, EmitAction emit,
            object?[] resolvedArgs, Dictionary<string, object?> localScope)
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
                if (MorphynRuntime.UnityCallback != null && emit.Arguments.Count > 0)
                {
                    string callbackName = resolvedArgs[0]?.ToString() ?? "";
                    object?[] callbackArgs = ObjectPools.Empty;
                    if (emit.Arguments.Count > 1)
                    {
                        callbackArgs = new object?[emit.Arguments.Count - 1];
                        Array.Copy(resolvedArgs, 1, callbackArgs, 0, callbackArgs.Length);
                    }
                    MorphynRuntime.UnityCallback(callbackName, callbackArgs);
                }
                return true;
            }

            return false;
        }
    }
}