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
        /// Parses a single 'has name: value' line and consumes the trailing semicolon.
        /// </summary>
        public static Parser<char, MorphynField> HasParser =>
            Pidgin.Parser.Map(
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

        /// <summary>
        /// Parses and event from the input.
        /// An event is used to implement entity behavior.
        /// It consists of a name and a list of statements.
        /// </summary>
        public static Parser<char, Event> EventParser =>
            Pidgin.Parser.Map((name, args, actions) => new Event
            {
                Name = name,
                Arguments = args.ToList(),
                Actions = actions.ToList()
            },
            Tok(String("on")).Then(Tok(Identifier)), 
            ArgsParser,
            Tok(Char('{')) 
                .Then(ActionParser.Many())
                .Before(Tok(Char('}'))) 
            );

        /// <summary>
        /// Parses a single 'emit event_name [args]' line and consumes the trailing semicolon.
        /// </summary>
        public static Parser<char, MorphynAction> EmitParser =>
            Pidgin.Parser.Map(
                (name, args) => new EmitAction
                {
                    EventName = name,
                    Arguments = args.ToList()
                } as MorphynAction,
                String("emit").Then(Skip).Then(Identifier),
                CallArgs
            ).Before(Char(';').Then(Skip));

        /// <summary>
        /// Parses a single 'check expression' line and consumes the trailing semicolon.
        /// </summary>
        public static Parser<char, MorphynAction> CheckParser =>
            String("check").Then(Skip)
                .Then(AnyCharExcept(';').ManyString())
                .Select(expr => new CheckAction() { Expression = expr.Trim() } as MorphynAction)
                .Before(Char(';').Then(Skip));
        
        public static Parser<char, MorphynAction> ActionParser =>
            OneOf(EmitParser, CheckParser).Labelled("action (emit or check)");
    }
}