using System;
using System.Collections.Generic;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        public static object EvaluateExpression(Entity entity, MorphynExpression expr, Dictionary<string, object> localScope)
        {
            return expr switch
            {
                LiteralExpression l => l.Value, 

                VariableExpression v => 
                    localScope.TryGetValue(v.Name, out var argVal) ? argVal :
                    (entity.Fields.TryGetValue(v.Name, out var fieldVal) 
                        ? fieldVal 
                        : throw new Exception($"Variable '{v.Name}' not found in entity '{entity.Name}' fields or event arguments.")),

                BinaryExpression b => EvaluateBinary(entity, b, localScope),
                _ => 0.0
            };
        }

        private static double EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, object> localScope)
        {
            double left = Convert.ToDouble(EvaluateExpression(entity, b.Left, localScope));
            double right = Convert.ToDouble(EvaluateExpression(entity, b.Right, localScope));

            return b.Operator switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right != 0 ? left / right : 0.0,
                "%" => right != 0 ? left % right : 0.0,
                _ => 0.0
            };
        }

        public static bool EvaluateCheck(Entity entity, CheckAction check, Dictionary<string, object> localScope)
        {
            object leftObj = EvaluateExpression(entity, check.Left, localScope);
            object rightObj = EvaluateExpression(entity, check.Right, localScope);

            if (IsNumeric(leftObj) && IsNumeric(rightObj))
            {
                double left = Convert.ToDouble(leftObj);
                double right = Convert.ToDouble(rightObj);

                return check.Operator switch
                {
                    ">" => left > right,
                    "<" => left < right,
                    "==" => Math.Abs(left - right) < 0.0001, 
                    "!=" => Math.Abs(left - right) > 0.0001,
                    ">=" => left >= right,
                    "<=" => left <= right,
                    _ => false
                };
            }

            return check.Operator switch
            {
                "==" => Equals(leftObj, rightObj),
                "!=" => !Equals(leftObj, rightObj),
                _ => false
            };
        }

        private static bool IsNumeric(object obj) => 
            obj is sbyte || obj is byte || obj is short || obj is ushort || 
            obj is int || obj is uint || obj is long || obj is ulong || 
            obj is float || obj is double || obj is decimal;
    }
}