using System;
using System.Collections.Generic;
using System.Globalization;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        public static object EvaluateExpression(Entity entity, MorphynExpression expr, 
            Dictionary<string, object> localScope, EntityData data)
        {
            return expr switch
            {
                LiteralExpression l => l.Value,

                VariableExpression v => 
                    localScope.TryGetValue(v.Name, out var argVal) ? argVal :
                        (entity.Fields.TryGetValue(v.Name, out var fieldVal) 
                            ? fieldVal 
                            : throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'")),

                BinaryExpression b => EvaluateBinary(entity, b, localScope, data), 

                BinaryLogicExpression bl => EvaluateLogic(entity, bl, localScope, data),
                
                UnaryLogicExpression u => EvaluateUnary(entity, u, localScope, data), 

                IndexAccessExpression idx => GetFromPool(entity, idx, localScope, data), 
        
                PoolPropertyExpression p => GetPoolProperty(entity, p, data), 

                _ => throw new Exception($"Unsupported expression: {expr.GetType().Name}")
            };
        }

        private static bool EvaluateLogic(Entity entity, BinaryLogicExpression b, Dictionary<string, object> localScope, EntityData data)
        {
            var left = (bool)EvaluateExpression(entity, b.Left, localScope, data);
            if (b.Operator == "or") return left || (bool)EvaluateExpression(entity, b.Right, localScope, data);
            if (b.Operator == "and") return left && (bool)EvaluateExpression(entity, b.Right, localScope, data);
            throw new Exception($"Unknown logic operator: {b.Operator}");
        }

        private static bool EvaluateUnary(Entity entity, UnaryLogicExpression u, Dictionary<string, object> localScope, EntityData data)
        {
            var val = (bool)EvaluateExpression(entity, u.Inner, localScope, data);
            return !val;
        }

        private static object GetPoolProperty(Entity entity, PoolPropertyExpression p, EntityData data)
        {
            if (entity.Fields.TryGetValue(p.TargetName, out var obj) && obj is MorphynPool pool)
            {
                if (p.Property == "count") return (double)pool.Values.Count;
                throw new Exception($"Property '{p.Property}' not supported for pools.");
            }

            if (data.Entities.TryGetValue(p.TargetName, out var externalEntity))
            {
                if (externalEntity.Fields.TryGetValue(p.Property, out var val))
                {
                    return val;
                }
                throw new Exception($"Field '{p.Property}' not found in external entity '{p.TargetName}'");
            }

            throw new Exception($"Entity or Pool '{p.TargetName}' not found.");
        }

        private static object GetFromPool(Entity entity, IndexAccessExpression idx, Dictionary<string, object> localScope, EntityData data)
        {
            if (entity.Fields.TryGetValue(idx.TargetName, out var val) && val is MorphynPool pool)
            {
                var evalIndex = EvaluateExpression(entity, idx.IndexExpr, localScope, data);
                int index = Convert.ToInt32(evalIndex) - 1; 
        
                if (index >= 0 && index < pool.Values.Count)
                    return pool.Values[index];
            
                throw new Exception($"Runtime Error: Index {index + 1} is out of bounds for pool '{idx.TargetName}'");
            }
            throw new Exception($"Target '{idx.TargetName}' is not a pool.");
        }

        private static object EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, object> localScope, EntityData data)
        {
            var leftObj = EvaluateExpression(entity, b.Left, localScope, data);
            var rightObj = EvaluateExpression(entity, b.Right, localScope, data);

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