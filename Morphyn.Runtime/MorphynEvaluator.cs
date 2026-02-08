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
        
                BinaryLogicExpression bl => EvaluateLogic(entity, bl, localScope),
        
                UnaryLogicExpression u => EvaluateUnary(entity, u, localScope),
        
                IndexAccessExpression idx => GetFromPool(entity, idx, localScope),
                PoolPropertyExpression p => GetPoolProperty(entity, p),
        
                _ => throw new Exception($"Unsupported expression: {expr.GetType().Name}")
            };
        }

        private static bool EvaluateLogic(Entity entity, BinaryLogicExpression b, Dictionary<string, object> localScope)
        {
            var left = (bool)EvaluateExpression(entity, b.Left, localScope);
            if (b.Operator == "or") return left || (bool)EvaluateExpression(entity, b.Right, localScope);
            if (b.Operator == "and") return left && (bool)EvaluateExpression(entity, b.Right, localScope);
            throw new Exception($"Unknown logic operator: {b.Operator}");
        }

        private static bool EvaluateUnary(Entity entity, UnaryLogicExpression u, Dictionary<string, object> localScope)
        {
            var val = (bool)EvaluateExpression(entity, u.Inner, localScope);
            return !val;
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

        private static object EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, object> localScope)
        {
            var leftObj = EvaluateExpression(entity, b.Left, localScope);
            var rightObj = EvaluateExpression(entity, b.Right, localScope);

            if (IsNumeric(leftObj) && IsNumeric(rightObj))
            {
                double l = Convert.ToDouble(leftObj, CultureInfo.InvariantCulture);
                double r = Convert.ToDouble(rightObj, CultureInfo.InvariantCulture);

                return b.Operator switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => Math.Abs(r) > 1e-9 ? l / r : 0.0,
                    "%" => l % r,
                    ">" => l > r,
                    "<" => l < r,
                    ">=" => l >= r,
                    "<=" => l <= r,
                    "==" => Math.Abs(l - r) < 1e-7,
                    "!=" => Math.Abs(l - r) > 1e-7,
                    _ => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }
    
            return b.Operator switch
            {
                "==" => Equals(leftObj, rightObj),
                "!=" => !Equals(leftObj, rightObj),
                _ => throw new Exception($"Operator {b.Operator} not supported for these types")
            };
        }

        private static bool IsNumeric(object? obj) => 
            obj is sbyte || obj is byte || obj is short || obj is ushort || 
            obj is int || obj is uint || obj is long || obj is ulong || 
            obj is float || obj is double || obj is decimal;
    }
}