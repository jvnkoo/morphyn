using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Parsers;

namespace Morphyn.Parser
{
    using System.Globalization;

    // Morphyn language parser
    public static partial class MorphynParser
    {
        private static readonly Tokenizer<MorphynToken> Tokenizer = MorphynTokenizer.Create();

        // Error handling callback, Unity can redirect this to Debug.LogError for better integration.
        public static Action<string>? OnError = message => Console.Error.WriteLine(message);

        public static EntityData ParseFile(string input)
        {
            try
            {
                var tokens = Tokenizer.Tokenize(input);
                var result = RootParser.Parse(tokens);
                return new EntityData(result);
            }
            catch (ParseException ex)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[Morphyn] Parse error");
                sb.AppendLine($"Position: line {ex.ErrorPosition.Line}, col {ex.ErrorPosition.Column}");
                sb.AppendLine($"Message: {ex.Message}");

                var lines = input.Split('\n');
                int lineIndex = Math.Clamp(ex.ErrorPosition.Line - 1, 0, lines.Length - 1);
                int colIndex = Math.Clamp(ex.ErrorPosition.Column - 1, 0, lines[lineIndex].Length);
                sb.AppendLine("Context:");
                sb.AppendLine(lines[lineIndex]);
                sb.AppendLine(new string(' ', colIndex) + "^");

                OnError?.Invoke(sb.ToString());
                throw new Exception("Morphyn parsing failed. See context above.");
            }
        }

        private static void PrintErrorContext(string input, Superpower.Model.Position position)
        {
            var lines = input.Split('\n');
            int lineIndex = Math.Clamp(position.Line - 1, 0, lines.Length - 1);
            int columnIndex = Math.Clamp(position.Column - 1, 0, lines[lineIndex].Length);
            Console.Error.WriteLine("Context:");
            if (lineIndex < lines.Length)
            {
                Console.Error.WriteLine(lines[lineIndex]);
                Console.Error.WriteLine(new string(' ', columnIndex) + "^");
            }
        }
    }
}