using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Parsers;

namespace Morphyn.Parser
{
    using System.Globalization;

    public static partial class MorphynParser
    {
        private static readonly Tokenizer<MorphynToken> Tokenizer = MorphynTokenizer.Create();

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
                Console.Error.WriteLine("Morphyn parse error");
                Console.Error.WriteLine($"Position: {ex.ErrorPosition}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                PrintErrorContext(input, ex.ErrorPosition);
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