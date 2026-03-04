using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            switch (expr.Kind)
            {
                case ExprKind.Literal:
                    return Unsafe.As<LiteralExpression>(expr).Value;

                case ExprKind.Variable:
                {
                    var v = Unsafe.As<VariableExpression>(expr);
                    if (localScope.TryGetValue(v.Name, out var argVal)) return argVal.ToObject();
                    if (entity.Fields.TryGetValue(v.Name, out var fieldVal)) return fieldVal.ToObject();
                    throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'");
                }

                case ExprKind.Binary:
                    return EvaluateBinaryToValue(entity, Unsafe.As<BinaryExpression>(expr), localScope, data).ToObject();

                case ExprKind.BinaryLogic:
                    return EvaluateLogic(entity, Unsafe.As<BinaryLogicExpression>(expr), localScope, data);

                case ExprKind.UnaryLogic:
                    return EvaluateUnary(entity, Unsafe.As<UnaryLogicExpression>(expr), localScope, data);

                case ExprKind.IndexAccess:
                    return GetFromPool(entity, Unsafe.As<IndexAccessExpression>(expr), localScope, data);

                case ExprKind.PoolProperty:
                    return GetPoolProperty(entity, Unsafe.As<PoolPropertyExpression>(expr), data);

                default:
                    throw new Exception($"Unsupported expression: {expr.GetType().Name}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue EvaluateToValue(Entity entity, MorphynExpression expr,
            Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            switch (expr.Kind)
            {
                case ExprKind.Literal:
                    return MorphynValue.FromObject(Unsafe.As<LiteralExpression>(expr).Value);

                case ExprKind.Variable:
                {
                    var v = Unsafe.As<VariableExpression>(expr);
                    if (localScope.TryGetValue(v.Name, out var argVal)) return argVal;
                    if (entity.Fields.TryGetValue(v.Name, out var fieldVal)) return fieldVal;
                    throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'");
                }

                case ExprKind.Binary:
                    return EvaluateBinaryToValue(entity, Unsafe.As<BinaryExpression>(expr), localScope, data);

                case ExprKind.BinaryLogic:
                    return MorphynValue.FromBool(EvaluateLogic(entity, Unsafe.As<BinaryLogicExpression>(expr), localScope, data));

                case ExprKind.UnaryLogic:
                    return MorphynValue.FromBool(EvaluateUnary(entity, Unsafe.As<UnaryLogicExpression>(expr), localScope, data));

                default:
                    return MorphynValue.FromObject(EvaluateExpression(entity, expr, localScope, data));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateLogic(Entity entity, BinaryLogicExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            var leftVal = EvaluateToValue(entity, b.Left, localScope, data);
            bool leftBool = leftVal.Kind == MorphynValueKind.Bool ? leftVal.BoolVal
                          : leftVal.Kind == MorphynValueKind.Null ? false
                          : Convert.ToBoolean(leftVal.ToObject());

            if (b.Operator == "or")
            {
                if (leftBool) return true;
                var rightVal = EvaluateToValue(entity, b.Right, localScope, data);
                return rightVal.Kind != MorphynValueKind.Null &&
                       (rightVal.Kind == MorphynValueKind.Bool ? rightVal.BoolVal : Convert.ToBoolean(rightVal.ToObject()));
            }

            if (b.Operator == "and")
            {
                if (!leftBool) return false;
                var rightVal = EvaluateToValue(entity, b.Right, localScope, data);
                return rightVal.Kind != MorphynValueKind.Null &&
                       (rightVal.Kind == MorphynValueKind.Bool ? rightVal.BoolVal : Convert.ToBoolean(rightVal.ToObject()));
            }

            throw new Exception($"Unknown logic operator: {b.Operator}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateUnary(Entity entity, UnaryLogicExpression u, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            var val = EvaluateToValue(entity, u.Inner, localScope, data);
            if (val.Kind == MorphynValueKind.Null) return true;
            if (val.Kind == MorphynValueKind.Bool) return !val.BoolVal;
            return !(bool)val.ToObject()!;
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
        private static object? GetFromPool(Entity entity, IndexAccessExpression idx, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            MorphynPool? pool = null;

            if (entity.Fields.TryGetValue(idx.TargetName, out var fieldVal))
                pool = fieldVal.ObjVal as MorphynPool;

            if (pool == null)
            {
                if (localScope.TryGetValue(idx.TargetName, out var scopeVal))
                    pool = scopeVal.ObjVal as MorphynPool;
            }

            if (pool == null)
                throw new Exception($"Target '{idx.TargetName}' is not a pool.");

            var idxVal = EvaluateToValue(entity, idx.IndexExpr, localScope, data);
            int index = (int)idxVal.NumVal - 1;

            if (index < 0 || index >= pool.Values.Count)
                throw new Exception($"Runtime Error: Index {index + 1} is out of bounds for pool '{idx.TargetName}'");

            return pool.Values[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MorphynValue EvaluateBinaryToValue(Entity entity, BinaryExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            if (TryGetDouble(entity, b.Left, localScope, data, out double l) &&
                TryGetDouble(entity, b.Right, localScope, data, out double r))
            {
                return b.Op switch
                {
                    BinaryOp.Add => MorphynValue.FromDouble(l + r),
                    BinaryOp.Sub => MorphynValue.FromDouble(l - r),
                    BinaryOp.Mul => MorphynValue.FromDouble(l * r),
                    BinaryOp.Div => MorphynValue.FromDouble(Math.Abs(r) > EPSILON ? l / r : 0.0),
                    BinaryOp.Mod => MorphynValue.FromDouble(l % r),
                    BinaryOp.Gt  => MorphynValue.FromBool(l > r),
                    BinaryOp.Lt  => MorphynValue.FromBool(l < r),
                    BinaryOp.Gte => MorphynValue.FromBool(l >= r),
                    BinaryOp.Lte => MorphynValue.FromBool(l <= r),
                    BinaryOp.Eq  => MorphynValue.FromBool(Math.Abs(l - r) < COMPARISON_EPSILON),
                    BinaryOp.Neq => MorphynValue.FromBool(Math.Abs(l - r) > COMPARISON_EPSILON),
                    _            => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }

            return MorphynValue.FromObject(EvaluateBinary(entity, b, localScope, data));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetDouble(Entity entity, MorphynExpression expr,
            Dictionary<string, MorphynValue> localScope, EntityData data, out double result)
        {
            switch (expr.Kind)
            {
                case ExprKind.Literal:
                {
                    var lit = Unsafe.As<LiteralExpression>(expr);
                    if (lit.Value is double d) { result = d; return true; }
                    result = 0; return false;
                }

                case ExprKind.Variable:
                {
                    var v = Unsafe.As<VariableExpression>(expr);
                    if (entity.Fields.TryGetValue(v.Name, out var fv) && fv.Kind == MorphynValueKind.Double)
                    {
                        result = fv.NumVal; return true;
                    }
                    if (localScope.TryGetValue(v.Name, out var sv) && sv.Kind == MorphynValueKind.Double)
                    {
                        result = sv.NumVal; return true;
                    }
                    result = 0; return false;
                }

                case ExprKind.Binary:
                {
                    var b = Unsafe.As<BinaryExpression>(expr);
                    if (TryGetDouble(entity, b.Left, localScope, data, out double bl) &&
                        TryGetDouble(entity, b.Right, localScope, data, out double br))
                    {
                        switch (b.Op)
                        {
                            case BinaryOp.Add: result = bl + br; return true;
                            case BinaryOp.Sub: result = bl - br; return true;
                            case BinaryOp.Mul: result = bl * br; return true;
                            case BinaryOp.Div: result = Math.Abs(br) > EPSILON ? bl / br : 0.0; return true;
                            case BinaryOp.Mod: result = bl % br; return true;
                        }
                    }
                    result = 0; return false;
                }

                default:
                    result = 0; return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            var leftObj = EvaluateExpression(entity, b.Left, localScope, data);
            var rightObj = EvaluateExpression(entity, b.Right, localScope, data);

            // Handle null comparisons for equality operators
            if (b.Op == BinaryOp.Eq || b.Op == BinaryOp.Neq)
            {
                if (leftObj == null || rightObj == null)
                    return b.Op == BinaryOp.Eq ? Equals(leftObj, rightObj) : !Equals(leftObj, rightObj);
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

                return b.Op switch
                {
                    BinaryOp.Add => l + r,
                    BinaryOp.Sub => l - r,
                    BinaryOp.Mul => l * r,
                    BinaryOp.Div => Math.Abs(r) > EPSILON ? l / r : 0.0,
                    BinaryOp.Mod => l % r,
                    BinaryOp.Gt  => l > r,
                    BinaryOp.Lt  => l < r,
                    BinaryOp.Gte => l >= r,
                    BinaryOp.Lte => l <= r,
                    BinaryOp.Eq  => Math.Abs(l - r) < COMPARISON_EPSILON,
                    BinaryOp.Neq => Math.Abs(l - r) > COMPARISON_EPSILON,
                    _            => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }

            if (leftObj is string || rightObj is string)
            {
                string l = leftObj?.ToString() ?? "";
                string r = rightObj?.ToString() ?? "";

                return b.Op switch
                {
                    BinaryOp.Add => l + r,
                    BinaryOp.Eq  => string.Equals(l, r, StringComparison.Ordinal),
                    BinaryOp.Neq => !string.Equals(l, r, StringComparison.Ordinal),
                    BinaryOp.Gt  => string.Compare(l, r, StringComparison.Ordinal) > 0,
                    BinaryOp.Lt  => string.Compare(l, r, StringComparison.Ordinal) < 0,
                    BinaryOp.Gte => string.Compare(l, r, StringComparison.Ordinal) >= 0,
                    BinaryOp.Lte => string.Compare(l, r, StringComparison.Ordinal) <= 0,
                    _            => throw new Exception($"Operator {b.Operator} not supported for strings")
                };
            }

            return b.Op switch
            {
                BinaryOp.Eq  => Equals(leftObj, rightObj),
                BinaryOp.Neq => !Equals(leftObj, rightObj),
                _            => throw new Exception($"Operator {b.Operator} not supported for these types")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNumeric(object? obj) =>
            obj is sbyte || obj is byte || obj is short || obj is ushort ||
            obj is int || obj is uint || obj is long || obj is ulong ||
            obj is float || obj is double || obj is decimal;
    }
}