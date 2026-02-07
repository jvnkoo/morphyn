namespace Morphyn.Parser
{
    public class MorphynExpression { }

    public class LiteralExpression : MorphynExpression
    {
        public object Value { get;  }
        public LiteralExpression(object value) => Value = value;
    }

    public class VariableExpression : MorphynExpression
    {
        public string Name { get;  }
        public VariableExpression(string name) => Name = name;
    }

    public class BinaryExpression : MorphynExpression
    {
        public string Operator { get; }
        public MorphynExpression Left { get;  }
        public MorphynExpression Right { get;  }

        public BinaryExpression(string op, MorphynExpression left, MorphynExpression right)
        {
            Operator = op;
            Left = left;
            Right = right;
        }
    }
}