using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Morphyn.Parser
{
    using Parser = Pidgin.Parser;

    /// <summary>
    /// Here we will implement the parser for the Morphyn language.
    /// It converts text from .morphyn files into ASTs(Entity and Event structures).
    /// </summary>
    public static class MorphynParser
    {
        // Parses a number from the input. 
        private static readonly Parser<char, int> Number =
            Digit.AtLeastOnceString().Select(int.Parse);
        
        // Parses an identifier from the input. 
        private static readonly Parser<char, string> Identifier =
            Letter.AtLeastOnceString();

        // Parses a single 'has name: value' line and consumes the trailing semicolon.
        public static readonly Parser<char, (string name, int value)> HasParser =
            Parser.Map(
                (name, value) => (name, value),
                String("has").Then(SkipWhitespaces).Then(Identifier),
                Char(':').Between(SkipWhitespaces).Then(Number)
            ).Before(Char(';').Then(SkipWhitespaces));

        // Parses multiple 'has' statements across the whole file.
        // SkipWhitespaces at the start allows the file to begin with empty lines.
        public static readonly Parser<char, IEnumerable<(string name, int value)>> AllHasParser =
            SkipWhitespaces.Then(HasParser.Many());
        
        /// <summary>
        /// Parses the entire file content and returns a list of fields.
        /// </summary>
        public static List<(string name, int value)> ParseFile(string input)
        {
            return AllHasParser.ParseOrThrow(input).ToList();
        }
        
        // TODO: add parse for entity, on, ->, check and other statements
    }
}