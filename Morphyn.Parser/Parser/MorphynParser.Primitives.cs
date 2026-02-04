using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Morphyn.Parser
{
    public static partial class MorphynParser
    {
        // Parses a number from the input. 
        private static readonly Parser<char, int> Number =
            Digit.AtLeastOnceString().Select(int.Parse);
        
        // Parses an identifier from the input. 
        private static readonly Parser<char, string> Identifier =
            Letter.AtLeastOnceString();
    }
}