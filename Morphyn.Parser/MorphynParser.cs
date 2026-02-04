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
        public static readonly Parser<char, MorphynField> HasParser =
            Parser.Map(
                (name, value) => new MorphynField(name, value),
                String("has").Then(SkipWhitespaces).Then(Identifier),
                Char(':').Between(SkipWhitespaces).Then(Number)
            ).Before(Char(';').Then(SkipWhitespaces));
        
        // =================================================================
        // =================== Entity parser ===============================
        // =================================================================

        /// <summary>
        /// Parses an entity from the input.
        /// </summary>
        public static readonly Parser<char, Entity> EntityParser =
            Parser.Map((name, fields) => 
                {
                    var entity = new Entity { Name = name };
                    foreach (var f in fields) 
                    {
                        entity.Fields[f.Name] = (int)f.Value; 
                    }
                    return entity;
                },
                String("entity").Then(SkipWhitespaces).Then(Identifier).Before(SkipWhitespaces),
                Char('{').Between(SkipWhitespaces)
                    .Then(HasParser.Many())
                    .Before(Char('}'))
            );
        
        // Parses the root of the file
        // EntityParser.Before(SkipWhitespaces).Many() parses multiple entities
        public static readonly Parser<char, IEnumerable<Entity>> RootParser =
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