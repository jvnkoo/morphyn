using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public static class MorphynEvaluator
    {
        public static bool EvaluateCheck(Entity entity, string expression)
        {
            var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return true;

            string fieldName = parts[0];
            string op = parts[1];

            if (!int.TryParse(parts[2], out int targetValue))
                return true;

            if (entity.Fields.TryGetValue(fieldName, out object val) && val is int currentVal)
            {
                bool result = op switch
                {
                    ">" => currentVal > targetValue,
                    "<" => currentVal < targetValue,
                    "==" => currentVal == targetValue,
                    "!=" => currentVal != targetValue,
                    ">=" => currentVal >= targetValue,
                    "<=" => currentVal <= targetValue,
                    _ => true
                };
                
                Console.WriteLine($"[Eval] {fieldName}({currentVal}) {op} {targetValue} => {result}");
                return result;
            }
            
            Console.WriteLine($"[Eval] Warning: Field '{fieldName}' not found or not an integer.");
            return false;
        }
    }
}