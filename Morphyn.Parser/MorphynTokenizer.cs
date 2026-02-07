using Superpower;
using Superpower.Tokenizers;
using Superpower.Parsers;
using static Superpower.Parse;

namespace Morphyn.Parser
{
    public enum MorphynToken
    {
        None,
        Identifier,
        Number,
        String,
        
        // Keywords
        Entity,
        Has,
        On,
        Emit,
        Check,
        
        // Symbols
        LeftBrace,
        RightBrace,
        LeftParen,
        RightParen,
        Comma,
        Colon,
        Dot,
        
        // Operators
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        DoubleEquals
    }

    public static class MorphynTokenizer
    {
        public static Tokenizer<MorphynToken> Create()
        {
            return new TokenizerBuilder<MorphynToken>()
                .Ignore(Span.WhiteSpace)
                .Ignore(Comment.CStyle)
                .Ignore(Comment.CPlusPlusStyle)
                .Ignore(Comment.ShellStyle)
                
                
                .Match(Span.EqualTo("entity"), MorphynToken.Entity)
                .Match(Span.EqualTo("has"), MorphynToken.Has)
                .Match(Span.EqualTo("on"), MorphynToken.On)
                .Match(Span.EqualTo("emit"), MorphynToken.Emit)
                .Match(Span.EqualTo("check"), MorphynToken.Check)
                
                .Match(Character.EqualTo('{'), MorphynToken.LeftBrace)
                .Match(Character.EqualTo('}'), MorphynToken.RightBrace)
                .Match(Character.EqualTo('('), MorphynToken.LeftParen)
                .Match(Character.EqualTo(')'), MorphynToken.RightParen)
                .Match(Character.EqualTo(','), MorphynToken.Comma)
                .Match(Character.EqualTo(':'), MorphynToken.Colon)
                .Match(Character.EqualTo('.'), MorphynToken.Dot)
                
                .Match(Span.EqualTo("=="), MorphynToken.DoubleEquals)
                .Match(Span.EqualTo("!="), MorphynToken.NotEquals)
                .Match(Span.EqualTo(">="), MorphynToken.GreaterThanOrEqual)
                .Match(Span.EqualTo("<="), MorphynToken.LessThanOrEqual)
                .Match(Character.EqualTo('>'), MorphynToken.GreaterThan)
                .Match(Character.EqualTo('<'), MorphynToken.LessThan)
                .Match(Character.EqualTo('='), MorphynToken.Equals)
                
                .Match(Numerics.Integer, MorphynToken.Number, requireDelimiters: true)
                .Match(QuotedString.CStyle, MorphynToken.String, requireDelimiters: true)
                
                .Match(Identifier.CStyle, MorphynToken.Identifier, requireDelimiters: true)
                
                .Build();
        }
    }
}