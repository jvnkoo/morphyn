using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Morphyn.Parser
{
    public static partial class MorphynParser
    {
        // Parses a number from the input. 
        private static Parser<char, int> Number =>
            Digit.AtLeastOnceString().Select(int.Parse);

        // Parses an identifier from the input. 
        private static Parser<char, string> Identifier =>
            Letter.AtLeastOnceString();

        private static Parser<char, T> Tok<T>(Parser<char, T> parser) =>
            parser.Between(Skip);

        private static Parser<char, Unit> Skip =>
            Whitespace.IgnoreResult().Or(Comment).SkipMany();

        // Parses arguments list: (arg1, arg2, arg3)
        private static Parser<char, IEnumerable<string>> ArgsParser =>
            Identifier.Separated(Tok(Char(',')))
                .Between(Tok(Char('(')), Tok(Char(')')));

        // Parses a statement in the event
        private static Parser<char, string> StatementParser =
            AnyCharExcept(';', '}').ManyString().Before(Char(';').Then(Skip));

        // Parser for string in quotes "string"
        private static Parser<char, string> StringLiteral =>
            Char('"').Then(AnyCharExcept('"').ManyString()).Before(Char('"'));
        
        // Parses arguments for emit call
        private static Parser<char, IEnumerable<object>> CallArgs =>
            OneOf(
                    Number.Cast<object>(),
                    StringLiteral.Cast<object>(),
                    Identifier.Cast<object>()
                ).Separated(Char(',').Between(Skip))
                .Between(Char('('), Char(')'));
    }
}