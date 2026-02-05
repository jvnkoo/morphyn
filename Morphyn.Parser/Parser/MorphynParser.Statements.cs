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
                String("has").Then(Skip).Then(Identifier),
                Char(':').Between(Skip).Then(Number).Cast<object>()
            ).Before(Char(';').Then(Skip));

        /// <summary>
        /// Parses an entity from the input.
        /// </summary>
        public static Parser<char, Entity> EntityParser =>
            String("entity").Then(Skip).Then(Identifier).Before(Skip)
                .Then(name => 
                        Char('{').Then(Skip)
                            .Then(OneOf(
                                Try(HasParser.Cast<object>()),
                                Try(EventParser.Cast<object>())
                            ).Between(Skip).Many()) 
                            .Before(Skip)           
                            .Before(Char('}')),
                    (name, members) =>
                    {
                        var entity = new Entity { Name = name };
                        foreach (var member in members)
                        {
                            if (member is MorphynField f) entity.Fields[f.Name] = f.Value;
                            if (member is Event e) entity.Events.Add(e);
                        }
                        return entity;
                    }
                );
        
        public static Parser<char, Unit> Comment =>
            (Char('#').IgnoreResult()
                .Or(String("//").IgnoreResult()))
            .Then(AnyCharExcept('\r','\n').SkipMany())
            .Then(
                Char('\n').IgnoreResult() 
                    .Or(End.IgnoreResult())                  
            );

    }
}