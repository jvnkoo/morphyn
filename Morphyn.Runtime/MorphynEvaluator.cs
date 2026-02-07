using System;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        public static object EvaluateExpression(Entity entity, MorphynExpression expr, List<object> eventArgs)
        {
            return expr switch
            {
                LiteralExpression l => l.Value, 
        
                VariableExpression v => 
                    (v.Name == "dt" && eventArgs.Count > 0) 
                        ? Convert.ToInt32(eventArgs[0]) 
                        : (entity.Fields.TryGetValue(v.Name, out var val) ? Convert.ToInt32(val) : 0),

                BinaryExpression b => EvaluateBinary(entity, b, eventArgs),
        
                _ => 0
            };
        }
        private static int EvaluateBinary(Entity entity, BinaryExpression b, List<object> args)
        {
            int left = Convert.ToInt32(EvaluateExpression(entity, b.Left, args));
            int right = Convert.ToInt32(EvaluateExpression(entity, b.Right, args));

            return b.Operator switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right != 0 ? left / right : 0,
                _ => 0
            };
        }

        public static bool EvaluateCheck(Entity entity, CheckAction check, List<object> args)
        {
            int left = Convert.ToInt32(EvaluateExpression(entity, check.Left, args));
            int right = Convert.ToInt32(EvaluateExpression(entity, check.Right, args));

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