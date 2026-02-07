using System;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        public static int EvaluateExpression(Entity entity, MorphynExpression expr, List<object> eventArgs)
        {
            return expr switch
            {
                LiteralExpression l => Convert.ToInt32(l.Value),
                            
                VariableExpression v => 
                    (v.Name == "dt" && eventArgs.Count > 0) ? Convert.ToInt32(eventArgs[0]) : 
                    entity.Fields.TryGetValue(v.Name, out var val) ? Convert.ToInt32(val) : 0,

                BinaryExpression b => Calculate(
                    EvaluateExpression(entity, b.Left, eventArgs),
                    b.Operator,
                    EvaluateExpression(entity, b.Right, eventArgs)
                ),
                _ => 0
            };
        }

        private static int Calculate(int left, string op, int right)
        {
            return op switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right == 0 ? 0 : left / right,
                "%" => right == 0 ? 0 : left % right,
                _ => 0
            };
        }

        public static bool EvaluateCheck(Entity entity, CheckAction check, List<object> args)
        {
            int left = EvaluateExpression(entity, check.Left, args);
            int right = EvaluateExpression(entity, check.Right, args);

            bool result = check.Operator switch
            {
                ">" => left > right,
                "<" => left < right,
                "==" => left == right,
                "!=" => left != right,
                ">=" => left >= right,
                "<=" => left <= right,
                _ => false
            };
            
            Console.WriteLine($"[Eval] Check: {left} {check.Operator} {right} => {result}");
            return result;
        }
    }
}