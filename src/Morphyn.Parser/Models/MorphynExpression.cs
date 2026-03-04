using System.Collections.Generic;

namespace Morphyn.Parser
{
    public enum BinaryOp : byte
    {
        Unknown,
        Add, Sub, Mul, Div, Mod,
        Gt, Lt, Gte, Lte, Eq, Neq
    }

    public enum ExprKind : byte
    {
        Literal, Variable, Binary, BinaryLogic, UnaryLogic, IndexAccess, PoolProperty
    }

    public class MorphynExpression
    {
        public ExprKind Kind;
    }

    public class IndexAccessExpression : MorphynExpression
    {
        public required string TargetName { get; set; }
        public required MorphynExpression IndexExpr { get; set; }
        public IndexAccessExpression() => Kind = ExprKind.IndexAccess;
    }

    public class PoolPropertyExpression : MorphynExpression
    {
        public string TargetName { get; set; } = default!;
        public string Property { get; set; } = default!;
        public PoolPropertyExpression() => Kind = ExprKind.PoolProperty;
    }

    public class LiteralExpression : MorphynExpression
    {
        public object? Value { get; }
        public LiteralExpression(object? value) { Value = value; Kind = ExprKind.Literal; }
    }

    public class VariableExpression : MorphynExpression
    {
        public string Name { get; }
        public VariableExpression(string name) { Name = name; Kind = ExprKind.Variable; }
    }

    public class BinaryExpression : MorphynExpression
    {
        public string Operator { get; }
        public BinaryOp Op { get; }
        public MorphynExpression Left { get; }
        public MorphynExpression Right { get; }

        public BinaryExpression(string op, MorphynExpression left, MorphynExpression right)
        {
            Kind = ExprKind.Binary;
            Operator = op;
            Left = left;
            Right = right;
            Op = op switch
            {
                "+"  => BinaryOp.Add,
                "-"  => BinaryOp.Sub,
                "*"  => BinaryOp.Mul,
                "/"  => BinaryOp.Div,
                "%"  => BinaryOp.Mod,
                ">"  => BinaryOp.Gt,
                "<"  => BinaryOp.Lt,
                ">=" => BinaryOp.Gte,
                "<=" => BinaryOp.Lte,
                "==" => BinaryOp.Eq,
                "!=" => BinaryOp.Neq,
                _    => BinaryOp.Unknown
            };
        }
    }

    public class BinaryLogicExpression : MorphynExpression
    {
        public MorphynExpression Left { get; }
        public string Operator { get; } // "and", "or"
        public MorphynExpression Right { get; }

        public BinaryLogicExpression(MorphynExpression left, string op, MorphynExpression right)
        {
            Kind = ExprKind.BinaryLogic;
            Left = left;
            Operator = op;
            Right = right;
        }
    }

    public class UnaryLogicExpression : MorphynExpression
    {
        public string Operator { get; } // "not"
        public MorphynExpression Inner { get; }

        public UnaryLogicExpression(string op, MorphynExpression inner)
        {
            Kind = ExprKind.UnaryLogic;
            Operator = op;
            Inner = inner;
        }
    }
}