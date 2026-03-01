using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    // Evaluates Morphyn expressions
    public static class MorphynEvaluator
    {
        private const double EPSILON = 1e-9;
        private const double COMPARISON_EPSILON = 1e-7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? EvaluateExpression(Entity entity, MorphynExpression expr,
            Dictionary<string, object?> localScope, EntityData data)
        {
            switch (expr)
            {
                case LiteralExpression l:
                    return l.Value;

                case VariableExpression v:
                    if (localScope.TryGetValue(v.Name, out var argVal))
                        return argVal;
                    if (entity.Fields.TryGetValue(v.Name, out var fieldVal))
                        return fieldVal.ToObject();
                    throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'");

                case BinaryExpression b:
                    return EvaluateBinaryFast(entity, b, localScope, data);

                case BinaryLogicExpression bl:
                    return EvaluateLogic(entity, bl, localScope, data);

                case UnaryLogicExpression u:
                    return EvaluateUnary(entity, u, localScope, data);

                case IndexAccessExpression idx:
                    return GetFromPool(entity, idx, localScope, data);

                case PoolPropertyExpression p:
                    return GetPoolProperty(entity, p, data);

                default:
                    throw new Exception($"Unsupported expression: {expr.GetType().Name}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue EvaluateToValue(Entity entity, MorphynExpression expr,
            Dictionary<string, object?> localScope, EntityData data)
        {
            switch (expr)
            {
                case LiteralExpression l:
                    return MorphynValue.FromObject(l.Value);

                case VariableExpression v:
                    if (localScope.TryGetValue(v.Name, out var argVal))
                        return MorphynValue.FromObject(argVal);
                    if (entity.Fields.TryGetValue(v.Name, out var fieldVal))
                        return fieldVal;
                    throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'");

                case BinaryExpression b:
                    return EvaluateBinaryToValue(entity, b, localScope, data);

                case BinaryLogicExpression bl:
                    return MorphynValue.FromBool(EvaluateLogic(entity, bl, localScope, data));

                case UnaryLogicExpression u:
                    return MorphynValue.FromBool(EvaluateUnary(entity, u, localScope, data));

                default:
                    return MorphynValue.FromObject(EvaluateExpression(entity, expr, localScope, data));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateLogic(Entity entity, BinaryLogicExpression b, Dictionary<string, object?> localScope, EntityData data)
        {
            var left = EvaluateExpression(entity, b.Left, localScope, data);
            if (left == null) return false;

            bool leftBool = (bool)left;

            if (b.Operator == "or")
            {
                if (leftBool) return true;
                var right = EvaluateExpression(entity, b.Right, localScope, data);
                return right != null && (bool)right;
            }

            if (b.Operator == "and")
            {
                if (!leftBool) return false;
                var right = EvaluateExpression(entity, b.Right, localScope, data);
                return right != null && (bool)right;
            }

            throw new Exception($"Unknown logic operator: {b.Operator}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateUnary(Entity entity, UnaryLogicExpression u, Dictionary<string, object?> localScope, EntityData data)
        {
            var val = EvaluateExpression(entity, u.Inner, localScope, data);
            if (val == null) return true;
            return !(bool)val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object? GetPoolProperty(Entity entity, PoolPropertyExpression p, EntityData data)
        {
            if (entity.Fields.TryGetValue(p.TargetName, out var fieldVal) && fieldVal.ObjVal is MorphynPool pool)
            {
                if (p.Property == "count") return (double)pool.Values.Count;
                throw new Exception($"Property '{p.Property}' not supported for pools.");
            }

            if (data.Entities.TryGetValue(p.TargetName, out var externalEntity))
            {
                if (externalEntity.Fields.TryGetValue(p.Property, out var val))
                    return val.ToObject();
                throw new Exception($"Field '{p.Property}' not found in external entity '{p.TargetName}'");
            }

            throw new Exception($"Entity or Pool '{p.TargetName}' not found.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object? GetFromPool(Entity entity, IndexAccessExpression idx, Dictionary<string, object?> localScope, EntityData data)
        {
            MorphynPool? pool = null;

            if (entity.Fields.TryGetValue(idx.TargetName, out var fieldVal))
                pool = fieldVal.ObjVal as MorphynPool;

            if (pool == null)
            {
                if (localScope.TryGetValue(idx.TargetName, out var scopeVal))
                    pool = scopeVal as MorphynPool;
            }

            if (pool == null)
                throw new Exception($"Target '{idx.TargetName}' is not a pool.");

            var evalIndex = EvaluateExpression(entity, idx.IndexExpr, localScope, data);
            if (evalIndex == null)
                throw new Exception($"Index expression evaluated to null for pool '{idx.TargetName}'");

            int index = Convert.ToInt32(evalIndex) - 1;

            if (index < 0 || index >= pool.Values.Count)
                throw new Exception($"Runtime Error: Index {index + 1} is out of bounds for pool '{idx.TargetName}'");

            return pool.Values[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object EvaluateBinaryFast(Entity entity, BinaryExpression b, Dictionary<string, object?> localScope, EntityData data)
        {
            if (TryGetDouble(entity, b.Left, localScope, data, out double lFast) &&
                TryGetDouble(entity, b.Right, localScope, data, out double rFast))
            {
                return b.Operator switch
                {
                    "+" => lFast + rFast,
                    "-" => lFast - rFast,
                    "*" => lFast * rFast,
                    "/" => Math.Abs(rFast) > EPSILON ? lFast / rFast : 0.0,
                    "%" => lFast % rFast,
                    ">" => lFast > rFast,
                    "<" => lFast < rFast,
                    ">=" => lFast >= rFast,
                    "<=" => lFast <= rFast,
                    "==" => Math.Abs(lFast - rFast) < COMPARISON_EPSILON,
                    "!=" => Math.Abs(lFast - rFast) > COMPARISON_EPSILON,
                    _ => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }

            return EvaluateBinary(entity, b, localScope, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MorphynValue EvaluateBinaryToValue(Entity entity, BinaryExpression b, Dictionary<string, object?> localScope, EntityData data)
        {
            if (TryGetDouble(entity, b.Left, localScope, data, out double l) &&
                TryGetDouble(entity, b.Right, localScope, data, out double r))
            {
                return b.Operator switch
                {
                    "+" => MorphynValue.FromDouble(l + r),
                    "-" => MorphynValue.FromDouble(l - r),
                    "*" => MorphynValue.FromDouble(l * r),
                    "/" => MorphynValue.FromDouble(Math.Abs(r) > EPSILON ? l / r : 0.0),
                    "%" => MorphynValue.FromDouble(l % r),
                    ">" => MorphynValue.FromBool(l > r),
                    "<" => MorphynValue.FromBool(l < r),
                    ">=" => MorphynValue.FromBool(l >= r),
                    "<=" => MorphynValue.FromBool(l <= r),
                    "==" => MorphynValue.FromBool(Math.Abs(l - r) < COMPARISON_EPSILON),
                    "!=" => MorphynValue.FromBool(Math.Abs(l - r) > COMPARISON_EPSILON),
                    _ => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }

            return MorphynValue.FromObject(EvaluateBinary(entity, b, localScope, data));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetDouble(Entity entity, MorphynExpression expr,
            Dictionary<string, object?> localScope, EntityData data, out double result)
        {
            if (expr is LiteralExpression lit)
            {
                if (lit.Value is double d) { result = d; return true; }
                result = 0; return false;
            }

            if (expr is VariableExpression v)
            {
                if (entity.Fields.TryGetValue(v.Name, out var fv) && fv.Kind == MorphynValueKind.Double)
                {
                    result = fv.NumVal;
                    return true;
                }
                if (localScope.TryGetValue(v.Name, out var sv) && sv is double sd)
                {
                    result = sd;
                    return true;
                }
                result = 0;
                return false;
            }

            if (expr is BinaryExpression b)
            {
                if (TryGetDouble(entity, b.Left, localScope, data, out double bl) &&
                    TryGetDouble(entity, b.Right, localScope, data, out double br))
                {
                    result = b.Operator switch
                    {
                        "+" => bl + br,
                        "-" => bl - br,
                        "*" => bl * br,
                        "/" => Math.Abs(br) > EPSILON ? bl / br : 0.0,
                        "%" => bl % br,
                        _ => double.NaN
                    };
                    if (!double.IsNaN(result)) return true;
                }
            }

            result = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, object?> localScope, EntityData data)
        {
            var leftObj = EvaluateExpression(entity, b.Left, localScope, data);
            var rightObj = EvaluateExpression(entity, b.Right, localScope, data);

            // Handle null comparisons for equality operators
            if (b.Operator == "==" || b.Operator == "!=")
            {
                if (leftObj == null || rightObj == null)
                    return b.Operator == "==" ? Equals(leftObj, rightObj) : !Equals(leftObj, rightObj);
            }
            else
            {
                // For non-equality operators, null is not allowed
                if (leftObj == null || rightObj == null)
                    throw new Exception($"Cannot perform operation '{b.Operator}' with null operand");
            }

            if (IsNumeric(leftObj) && IsNumeric(rightObj))
            {
                double l = Convert.ToDouble(leftObj);
                double r = Convert.ToDouble(rightObj);

                return b.Operator switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => Math.Abs(r) > EPSILON ? l / r : 0.0,
                    "%" => l % r,
                    ">" => l > r,
                    "<" => l < r,
                    ">=" => l >= r,
                    "<=" => l <= r,
                    "==" => Math.Abs(l - r) < COMPARISON_EPSILON,
                    "!=" => Math.Abs(l - r) > COMPARISON_EPSILON,
                    _ => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }

            if (leftObj is string || rightObj is string)
            {
                string l = leftObj?.ToString() ?? "";
                string r = rightObj?.ToString() ?? "";

                return b.Operator switch
                {
                    "+" => l + r,
                    "==" => string.Equals(l, r, StringComparison.Ordinal),
                    "!=" => !string.Equals(l, r, StringComparison.Ordinal),
                    ">" => string.Compare(l, r, StringComparison.Ordinal) > 0,
                    "<" => string.Compare(l, r, StringComparison.Ordinal) < 0,
                    ">=" => string.Compare(l, r, StringComparison.Ordinal) >= 0,
                    "<=" => string.Compare(l, r, StringComparison.Ordinal) <= 0,
                    _ => throw new Exception($"Operator {b.Operator} not supported for strings")
                };
            }

            return b.Operator switch
            {
                "==" => Equals(leftObj, rightObj),
                "!=" => !Equals(leftObj, rightObj),
                _ => throw new Exception($"Operator {b.Operator} not supported for these types")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNumeric(object? obj) =>
            obj is sbyte || obj is byte || obj is short || obj is ushort ||
            obj is int || obj is uint || obj is long || obj is ulong ||
            obj is float || obj is double || obj is decimal;
    }
}