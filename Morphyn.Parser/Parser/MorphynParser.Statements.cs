using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using System.Collections.Generic;

namespace Morphyn.Parser
{
    using System.Security.AccessControl;
    using Parser = Pidgin.Parser;

    public static partial class MorphynParser
    {
        /// <summary>
        /// Parses a single 'has name: value' line and consumes the trailing semicolon.
        /// </summary>
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

        /// <summary>
        /// Parses an event from the input.
        /// An event is used to implement entity behavior.
        /// It consists of a name and a list of statements.
        /// </summary>
        public static Parser<char, Event> EventParser =>
            Parser.Map((name, args, statement) => new Event
            {
                Name = name,
                Arguments = args.ToList(),
                Statements = statement.ToList()
            },
            Tok(String("on")).Then(Tok(Identifier)), 
            ArgsParser,
            Tok(Char('{')) 
                .Then(StatementParser.Many())
                .Before(Tok(Char('}'))) 
            );

        /// <summary>
        /// Parses a single 'emit event_name [args]' line and consumes the trailing semicolon.
        /// </summary>
        public static Parser<char, MorphynAction> EmitParser =>
            String("emit").Then(Skip).Then(Identifier)
                .Then(CallArgs, (name, args) => new EmitAction
                {
                    EventName = name,
                    Arguments = args.ToList()
                } as MorphynAction)
                .Before(Char(';').Then(Skip));

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