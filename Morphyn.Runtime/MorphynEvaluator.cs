using System;
using System.Collections.Generic;
using System.Globalization;
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
                        : throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'")),

                BinaryExpression b => EvaluateBinary(entity, b, localScope),
                
                IndexAccessExpression idx => GetFromPool(entity, idx, localScope),
                
                PoolPropertyExpression p => GetPoolProperty(entity, p),
                
                _ => throw new Exception($"Unsupported expression: {expr.GetType().Name}")
            };
        }

        private static object GetPoolProperty(Entity entity, PoolPropertyExpression p)
        {
            if (entity.Fields.TryGetValue(p.TargetName, out var val) && val is MorphynPool pool)
            {
                if (p.Property.ToLower() == "count") 
                    return (double)pool.Values.Count; 
            
                throw new Exception($"Property '{p.Property}' not supported.");
            }
            throw new Exception($"Pool '{p.TargetName}' not found.");
        }

        private static object GetFromPool(Entity entity, IndexAccessExpression idx, Dictionary<string, object> localScope)
        {
            if (entity.Fields.TryGetValue(idx.TargetName, out var val) && val is MorphynPool pool)
            {
                var evalIndex = EvaluateExpression(entity, idx.IndexExpr, localScope);
                int index = Convert.ToInt32(evalIndex) - 1; // 1-based indexing
        
                if (index >= 0 && index < pool.Values.Count)
                    return pool.Values[index];
            
                throw new Exception($"Runtime Error: Index {index + 1} is out of bounds for pool '{idx.TargetName}' (Size: {pool.Values.Count})");
            }
            throw new Exception($"Target '{idx.TargetName}' is not a pool.");
        }

        private static double EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, object> localScope)
        {
            double left = Convert.ToDouble(EvaluateExpression(entity, b.Left, localScope), CultureInfo.InvariantCulture);
            double right = Convert.ToDouble(EvaluateExpression(entity, b.Right, localScope), CultureInfo.InvariantCulture);

            return b.Operator switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => Math.Abs(right) > 1e-9 ? left / right : 0.0,
                "%" => Math.Abs(right) > 1e-9 ? left % right : 0.0,
                _ => throw new Exception($"Unknown operator: {b.Operator}")
            };
        }

        public static bool EvaluateCheck(Entity entity, CheckAction check, Dictionary<string, object> localScope)
        {
            object leftObj = EvaluateExpression(entity, check.Left, localScope);
            object rightObj = EvaluateExpression(entity, check.Right, localScope);

            if (IsNumeric(leftObj) && IsNumeric(rightObj))
            {
                double left = Convert.ToDouble(leftObj, CultureInfo.InvariantCulture);
                double right = Convert.ToDouble(rightObj, CultureInfo.InvariantCulture);

                return check.Operator switch
                {
                    ">" => left > right,
                    "<" => left < right,
                    "==" => Math.Abs(left - right) < 1e-7, 
                    "!=" => Math.Abs(left - right) > 1e-7,
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

        private static bool IsNumeric(object? obj) => 
            obj is sbyte || obj is byte || obj is short || obj is ushort || 
            obj is int || obj is uint || obj is long || obj is ulong || 
            obj is float || obj is double || obj is decimal;
    }
}