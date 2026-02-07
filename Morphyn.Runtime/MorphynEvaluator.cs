using System;
using System.Collections.Generic;
using System.Linq;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        public static object EvaluateExpression(Entity entity, MorphynExpression expr, Event ev, List<object> eventArgs)
        {
            return expr switch
            {
                LiteralExpression l => l.Value, 

                VariableExpression v => 
                    GetArgumentValue(v.Name, ev, eventArgs) ?? 
                    (entity.Fields.TryGetValue(v.Name, out var val) 
                        ? val 
                        : throw new Exception($"Variable '{v.Name}' not found in event '{ev.Name}' or entity '{entity.Name}'")),

                BinaryExpression b => EvaluateBinary(entity, b, ev, eventArgs),
                _ => 0.0
            };
        }

        private static object? GetArgumentValue(string name, Event ev, List<object> args)
        {
            int index = ev.Parameters.IndexOf(name);
            if (index != -1 && index < args.Count) return args[index];
            return null;
        }

        private static double EvaluateBinary(Entity entity, BinaryExpression b, Event ev, List<object> args)
        {
            double left = Convert.ToDouble(EvaluateExpression(entity, b.Left, ev, args));
            double right = Convert.ToDouble(EvaluateExpression(entity, b.Right, ev, args));

            return b.Operator switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right != 0 ? left / right : 0.0,
                _ => 0.0
            };
        }

        public static bool EvaluateCheck(Entity entity, CheckAction check, Event ev, List<object> args)
        {
            double left = Convert.ToDouble(EvaluateExpression(entity, check.Left, ev, args));
            double right = Convert.ToDouble(EvaluateExpression(entity, check.Right, ev, args));

            bool result = check.Operator switch
            {
                ">" => left > right,
                "<" => left < right,
                "==" => Math.Abs(left - right) < 0.0001, 
                "!=" => Math.Abs(left - right) > 0.0001,
                ">=" => left >= right,
                "<=" => left <= right,
                _ => false
            };
    
            return result;
        }
    }
}