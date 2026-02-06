using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Morphyn.Parser
{
    /// <summary>
    /// Here we will implement the parser for the Morphyn language.
    /// It converts text from .morphyn files into ASTs(Entity and Event structures).
    /// </summary>
    public static partial class MorphynParser
    {
        // Parses the root of the file
        // EntityParser.Before(SkipWhitespaces).Many() parses multiple entities
        public static Parser<char, IEnumerable<Entity>> RootParser =>
            Tok(EntityParser).Many();

        /// <summary>
        /// Parses a .morphyn file and returns parsed data.
        /// File contains one or more entities, each of which is parsed into an Entity instance.
        /// If parsing fails, it throws an exception with details about the error.
        /// </summary>
        public static EntityData ParseFile(string input)
        {
            var result = RootParser.Parse(input);

            if (result.Success)
            {
                return new EntityData(result.Value);
            }

            var error = result.Error;
            Console.Error.WriteLine("Morphyn parse error");
            Console.Error.WriteLine($"Position : line {error.ErrorPos.Line}, column {error.ErrorPos.Col}");
            PrintErrorContext(input, error.ErrorPos.Line, error.ErrorPos.Col);

            throw new Exception("Morphyn parsing failed. Look at the context above.");
        }

        private static void PrintErrorContext(string input, int line, int column)
        {
            var lines = input.Split('\n');
            var l = Math.Clamp(line - 1, 0, lines.Length - 1);
            var c = Math.Clamp(column - 1, 0, lines[l].Length);

            Console.Error.WriteLine("Context:");
            if (l < lines.Length)
            {
                Console.Error.WriteLine(lines[l]);
                Console.Error.WriteLine(new string(' ', c) + "^");
            }
        }
    }
}