using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using System.Collections.Generic;

namespace Morphyn.Parser
{
    using Parser = Pidgin.Parser;

    public static partial class MorphynParser
    {
        // Parses a single 'has name: value' line and consumes the trailing semicolon.
        public static Parser<char, MorphynField> HasParser =>
            Parser.Map(
                (name, value) => new MorphynField(name, value),
                String("has").Then(SkipWhitespaces).Then(Identifier),
                Char(':').Between(SkipWhitespaces).Then(Number)
            ).Before(Char(';').Then(SkipWhitespaces));

        /// <summary>
        /// Parses an entity from the input.
        /// </summary>
        public static Parser<char, Entity> EntityParser =>
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
    }
}