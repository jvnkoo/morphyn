using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        private const double EPSILON = 1e-9;
        private const double COMPARISON_EPSILON = 1e-7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? EvaluateExpression(Entity entity, MorphynExpression expr,
            Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            return EvaluateToValue(entity, expr, localScope, data).ToObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue EvaluateToValue(Entity entity, MorphynExpression expr,
            Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            switch (expr.Kind)
            {
                case ExprKind.Literal:
                {
                    var val = Unsafe.As<LiteralExpression>(expr).Value;
                    // Promote ints and floats to double immediately to hit the zero-alloc fast path
                    if (val is int i) return MorphynValue.FromDouble(i);
                    if (val is float f) return MorphynValue.FromDouble(f);
                    if (val is double d) return MorphynValue.FromDouble(d);
                    return MorphynValue.FromObject(val);
                }

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

                case ExprKind.IndexAccess:
                    return GetFromPool(entity, Unsafe.As<IndexAccessExpression>(expr), localScope, data);

                case ExprKind.PoolProperty:
                    return GetPoolProperty(entity, Unsafe.As<PoolPropertyExpression>(expr), localScope, data);

                default:
                    throw new Exception($"Unknown expression kind: {expr.Kind}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateLogic(Entity entity, BinaryLogicExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            var left = EvaluateToValue(entity, b.Left, localScope, data);
            bool leftBool = left.Kind == MorphynValueKind.Bool ? left.BoolVal : left.Kind != MorphynValueKind.Null;

            if (b.Operator == "and")
            {
                if (!leftBool) return false;
                var right = EvaluateToValue(entity, b.Right, localScope, data);
                return right.Kind == MorphynValueKind.Bool ? right.BoolVal : right.Kind != MorphynValueKind.Null;
            }
            if (b.Operator == "or")
            {
                if (leftBool) return true;
                var right = EvaluateToValue(entity, b.Right, localScope, data);
                return right.Kind == MorphynValueKind.Bool ? right.BoolVal : right.Kind != MorphynValueKind.Null;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateUnary(Entity entity, UnaryLogicExpression u, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            var val = EvaluateToValue(entity, u.Inner, localScope, data);
            bool boolVal = val.Kind == MorphynValueKind.Bool ? val.BoolVal : val.Kind != MorphynValueKind.Null;
            return !boolVal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MorphynValue GetFromPool(Entity entity, IndexAccessExpression idx, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            MorphynPool? pool = null;
            if (entity.Fields.TryGetValue(idx.TargetName, out var fv) && fv.ObjVal is MorphynPool fp)
                pool = fp;
            
            if (pool == null && localScope.TryGetValue(idx.TargetName, out var sv) && sv.ObjVal is MorphynPool sp)
                pool = sp;

            if (pool == null) throw new Exception($"Pool '{idx.TargetName}' not found.");

            var indexVal = EvaluateToValue(entity, idx.IndexExpr, localScope, data);
            int i = 0;
            if (indexVal.Kind == MorphynValueKind.Double) i = (int)indexVal.NumVal - 1;
            else if (IsNumeric(indexVal.ToObject())) i = Convert.ToInt32(indexVal.ToObject()) - 1;

            if (i < 0 || i >= pool.Values.Count) return MorphynValue.Null;
            
            var item = pool.Values[i];

            // Prevent double-boxing if the pool already contains a MorphynValue (happens after swapping)
            if (item is MorphynValue mv) return mv;
            
            // Promote ints/floats parsed by the parser directly to double to ensure fast-path execution
            if (item is int numInt) return MorphynValue.FromDouble(numInt);
            if (item is float numFloat) return MorphynValue.FromDouble(numFloat);
            if (item is double numDouble) return MorphynValue.FromDouble(numDouble);

            return MorphynValue.FromObject(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MorphynValue GetPoolProperty(Entity entity, PoolPropertyExpression expr, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            MorphynPool? pool = null;
            if (entity.Fields.TryGetValue(expr.TargetName, out var fv) && fv.ObjVal is MorphynPool fp)
                pool = fp;
            if (pool == null && localScope.TryGetValue(expr.TargetName, out var sv) && sv.ObjVal is MorphynPool sp)
                pool = sp;

            if (pool == null) throw new Exception($"Pool '{expr.TargetName}' not found.");

            return expr.Property switch
            {
                "count" => MorphynValue.FromDouble(pool.Values.Count),
                _ => throw new Exception($"Unknown pool property: {expr.Property}")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue EvaluateBinaryToValue(Entity entity, BinaryExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            var left = EvaluateToValue(entity, b.Left, localScope, data);
            var right = EvaluateToValue(entity, b.Right, localScope, data);

            // 1. Fast path for strict doubles (Zero-Alloc)
            if (left.Kind == MorphynValueKind.Double && right.Kind == MorphynValueKind.Double)
            {
                double l = left.NumVal;
                double r = right.NumVal;

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
                    BinaryOp.Neq => MorphynValue.FromBool(Math.Abs(l - r) >= COMPARISON_EPSILON),
                    _ => throw new Exception($"Operator {b.Op} not supported for strict numbers")
                };
            }

            var leftObj = left.ToObject();
            var rightObj = right.ToObject();

            // 2. Fallback path for any other numeric types
            if (IsNumeric(leftObj) && IsNumeric(rightObj))
            {
                double l = Convert.ToDouble(leftObj);
                double r = Convert.ToDouble(rightObj);

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
                    BinaryOp.Neq => MorphynValue.FromBool(Math.Abs(l - r) >= COMPARISON_EPSILON),
                    _            => throw new Exception($"Operator {b.Op} not supported for generic numbers")
                };
            }

            // 3. String operations
            if (leftObj is string || rightObj is string)
            {
                string lStr = leftObj?.ToString() ?? "";
                string rStr = rightObj?.ToString() ?? "";

                return b.Op switch
                {
                    BinaryOp.Add => MorphynValue.FromObject(lStr + rStr),
                    BinaryOp.Eq  => MorphynValue.FromBool(string.Equals(lStr, rStr, StringComparison.Ordinal)),
                    BinaryOp.Neq => MorphynValue.FromBool(!string.Equals(lStr, rStr, StringComparison.Ordinal)),
                    BinaryOp.Gt  => MorphynValue.FromBool(string.Compare(lStr, rStr, StringComparison.Ordinal) > 0),
                    BinaryOp.Lt  => MorphynValue.FromBool(string.Compare(lStr, rStr, StringComparison.Ordinal) < 0),
                    BinaryOp.Gte => MorphynValue.FromBool(string.Compare(lStr, rStr, StringComparison.Ordinal) >= 0),
                    BinaryOp.Lte => MorphynValue.FromBool(string.Compare(lStr, rStr, StringComparison.Ordinal) <= 0),
                    _            => throw new Exception($"Operator {b.Op} not supported for strings")
                };
            }

            // 4. Fallback for objects
            return b.Op switch
            {
                BinaryOp.Eq  => MorphynValue.FromBool(Equals(leftObj, rightObj)),
                BinaryOp.Neq => MorphynValue.FromBool(!Equals(leftObj, rightObj)),
                _            => throw new Exception($"Operator {b.Op} not supported for these types")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? EvaluateBinaryFast(Entity entity, BinaryExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            return EvaluateBinaryToValue(entity, b, localScope, data).ToObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetDouble(Entity entity, MorphynExpression expr,
            Dictionary<string, MorphynValue> localScope, EntityData data, out double result)
        {
            var val = EvaluateToValue(entity, expr, localScope, data);
            
            if (val.Kind == MorphynValueKind.Double)
            {
                result = val.NumVal;
                return true;
            }
            
            var obj = val.ToObject();
            if (IsNumeric(obj))
            {
                result = Convert.ToDouble(obj);
                return true;
            }
            
            result = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, MorphynValue> localScope, EntityData data)
        {
            return EvaluateBinaryToValue(entity, b, localScope, data).ToObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNumeric(object? obj) =>
            obj is sbyte || obj is byte || obj is short || obj is ushort ||
            obj is int || obj is uint || obj is long || obj is ulong ||
            obj is float || obj is double || obj is decimal;
    }
}