using System.Collections.Generic;

namespace Morphyn.Parser
{
    public class MorphynExpression
    {
    }

    public class IndexAccessExpression : MorphynExpression
    {
        public required string TargetName { get; set; }
        public required MorphynExpression IndexExpr { get; set; }
    }

    public class PoolPropertyExpression : MorphynExpression
    {
        public string TargetName { get; set; } = default!;
        public string Property { get; set; } = default!;
    }

    public class LiteralExpression : MorphynExpression
    {
        public object? Value { get; }
        public LiteralExpression(object? value) => Value = value;
    }

    public class VariableExpression : MorphynExpression
    {
        public string Name { get; }
        public VariableExpression(string name) => Name = name;
    }

    public class BinaryExpression : MorphynExpression
    {
        public string Operator { get; }
        public MorphynExpression Left { get; }
        public MorphynExpression Right { get; }

        public BinaryExpression(string op, MorphynExpression left, MorphynExpression right)
        {
            Operator = op;
            Left = left;
            Right = right;
        }
    }

    public class BinaryLogicExpression : MorphynExpression
    {
        public MorphynExpression Left { get; }
        public string Operator { get; } // "and", "or"
        public MorphynExpression Right { get; }

        public BinaryLogicExpression(MorphynExpression left, string op, MorphynExpression right)
        {
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
            Operator = op;
            Inner = inner;
        }
    }
}