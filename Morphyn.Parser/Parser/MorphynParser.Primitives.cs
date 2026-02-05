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
    }
}