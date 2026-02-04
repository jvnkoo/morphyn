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
            SkipWhitespaces.Then(EntityParser.Before(SkipWhitespaces).Many());
        
        /// <summary>
        /// Parses the entire file content and returns a list of fields.
        /// </summary>
        public static List<Entity> ParseFile(string input)
        {
            return RootParser.ParseOrThrow(input).ToList();
        }
        
        // TODO: add parse for on, ->, check and other statements
    }
}