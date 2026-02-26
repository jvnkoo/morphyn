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
        Double,
        Number,
        String,
        True,
        False,
        Null,
        
        // Keywords
        Entity,
        Has,
        On,
        Emit,
        When,
        Unwhen,
        Check,
        Pool,
        
        // Symbols
        LeftBrace,
        RightBrace,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Comma,
        Colon,
        Dot,
        
        // Operators
        Or,
        And,
        Not,
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        DoubleEquals,
        
        // Math operators
        Plus,
        Minus,
        Asterisk,
        Slash,
        Percent,
        Arrow
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
            
            .Match(Span.EqualTo("entity"), MorphynToken.Entity, requireDelimiters: true)
            .Match(Span.EqualTo("has"), MorphynToken.Has, requireDelimiters: true)
            .Match(Span.EqualTo("on"), MorphynToken.On, requireDelimiters: true)
            .Match(Span.EqualTo("emit"), MorphynToken.Emit, requireDelimiters: true)
            .Match(Span.EqualTo("when"), MorphynToken.When, requireDelimiters: true)
            .Match(Span.EqualTo("unwhen"), MorphynToken.Unwhen, requireDelimiters: true)
            .Match(Span.EqualTo("check"), MorphynToken.Check, requireDelimiters: true)
            .Match(Span.EqualTo("pool"), MorphynToken.Pool, requireDelimiters: true)
            .Match(Span.EqualTo("or"), MorphynToken.Or, requireDelimiters: true)
            .Match(Span.EqualTo("and"), MorphynToken.And, requireDelimiters: true)
            .Match(Span.EqualTo("not"), MorphynToken.Not, requireDelimiters: true)
            
            .Match(Span.EqualTo("true"), MorphynToken.True, requireDelimiters: true)
            .Match(Span.EqualTo("false"), MorphynToken.False, requireDelimiters: true)
            .Match(Span.EqualTo("null"), MorphynToken.Null, requireDelimiters: true)  
            
            .Match(Character.EqualTo('{'), MorphynToken.LeftBrace)
            .Match(Character.EqualTo('}'), MorphynToken.RightBrace)
            .Match(Character.EqualTo('('), MorphynToken.LeftParen)
            .Match(Character.EqualTo(')'), MorphynToken.RightParen)
            .Match(Character.EqualTo('['), MorphynToken.LeftBracket)
            .Match(Character.EqualTo(']'), MorphynToken.RightBracket)
            .Match(Character.EqualTo(','), MorphynToken.Comma)
            .Match(Character.EqualTo(':'), MorphynToken.Colon)
            .Match(Character.EqualTo('.'), MorphynToken.Dot)
            
            .Match(Span.EqualTo("=="), MorphynToken.DoubleEquals)
            .Match(Span.EqualTo("!="), MorphynToken.NotEquals)
            .Match(Span.EqualTo(">="), MorphynToken.GreaterThanOrEqual)
            .Match(Span.EqualTo("<="), MorphynToken.LessThanOrEqual)
            .Match(Span.EqualTo("->"), MorphynToken.Arrow)
            .Match(Character.EqualTo('>'), MorphynToken.GreaterThan)
            .Match(Character.EqualTo('<'), MorphynToken.LessThan)
            .Match(Character.EqualTo('='), MorphynToken.Equals)
            .Match(Character.EqualTo('+'), MorphynToken.Plus)
            .Match(Character.EqualTo('-'), MorphynToken.Minus)
            .Match(Character.EqualTo('*'), MorphynToken.Asterisk)
            .Match(Character.EqualTo('/'), MorphynToken.Slash)
            .Match(Character.EqualTo('%'), MorphynToken.Percent)
            
            .Match(Numerics.Decimal, MorphynToken.Double) 
            .Match(Numerics.Integer, MorphynToken.Number, requireDelimiters: true)
            
            .Match(QuotedString.CStyle, MorphynToken.String, requireDelimiters: true)
            
            .Match(Identifier.CStyle, MorphynToken.Identifier, requireDelimiters: true)
            
            .Build();
        }
    }
}