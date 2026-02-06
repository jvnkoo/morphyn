using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using System.Collections.Generic;
using System.Linq;

namespace Morphyn.Parser
{
    public static partial class MorphynParser
    {
        /// <summary>
        /// Parses an identifier. An identifier is a string of one or more letters or digits,
        /// possibly starting with an underscore.
        /// </summary>
        private static Parser<char, string> Identifier =>
            Pidgin.Parser.Map((first, rest) => first + rest, 
                Letter.Or(Char('_')), 
                LetterOrDigit.Or(Char('_')).ManyString());

        // Parses a number from the input
        private static Parser<char, int> Number =>
            Digit.AtLeastOnceString().Select(int.Parse);
        
        /// <summary>
        /// Parses a token. A token is a non-whitespace, non-comment character.
        /// </summary>
        private static Parser<char, T> Tok<T>(Parser<char, T> parser) =>
            parser.Between(Skip);

        private static Parser<char, Unit> Skip =>
            Whitespace.IgnoreResult().Or(Comment).SkipMany();

        private static Parser<char, IEnumerable<string>> ArgsParser =>
            Tok(Identifier).Separated(Tok(Char(',')))
                .Or(Pidgin.Parser<char>.Return(Enumerable.Empty<string>()))
                .Between(Tok(Char('(')), Tok(Char(')')));

        /// <summary>
        /// Parser for string in quotes "string"
        /// </summary>
        private static Parser<char, string> StringLiteral =>
            Char('"').Then(AnyCharExcept('"').ManyString()).Before(Char('"'));
        
        /// <summary>
        /// Parses argument for emit call
        /// </summary>
        private static Parser<char, IEnumerable<object>> CallArgs =>
            OneOf(
                    Number.Cast<object>(),
                    StringLiteral.Cast<object>(),
                    Identifier.Cast<object>()
                ).Separated(Tok(Char(',')))
                .Or(Pidgin.Parser<char>.Return(Enumerable.Empty<object>()))
                .Between(Tok(Char('(')), Tok(Char(')')));
    }
}