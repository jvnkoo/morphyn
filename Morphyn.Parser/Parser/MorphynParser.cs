using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Parsers;

namespace Morphyn.Parser
{
    public static class MorphynParser
    {
        private static readonly Tokenizer<MorphynToken> Tokenizer = MorphynTokenizer.Create();

        // Parse identifier token
        private static TokenListParser<MorphynToken, string> Identifier =>
            Token.EqualTo(MorphynToken.Identifier).Select(t => t.ToStringValue());

        // Parse number token
        private static TokenListParser<MorphynToken, int> Number =>
            Token.EqualTo(MorphynToken.Number).Select(t => int.Parse(t.ToStringValue()));

        // Parse string token (removes quotes)
        private static TokenListParser<MorphynToken, string> String =>
            Token.EqualTo(MorphynToken.String).Select(t => 
            {
                var str = t.ToStringValue();
                return str.Substring(1, str.Length - 2);
            });

        // Parse field declaration: identifier : number
        // Maybe make "has" optional
        private static TokenListParser<MorphynToken, KeyValuePair<string, object>> FieldDeclaration =>
            (from hasKeyWord in Token.EqualTo(MorphynToken.Has)
                from name in Identifier
                from colon in Token.EqualTo(MorphynToken.Colon)
                from value in Number
                select new KeyValuePair<string, object>(name, value)).Try();

        // Parse parameter list: (param1, param2, ...)
        private static TokenListParser<MorphynToken, string[]> ParameterList =>
            Identifier.ManyDelimitedBy(Token.EqualTo(MorphynToken.Comma))
                .Between(Token.EqualTo(MorphynToken.LeftParen), Token.EqualTo(MorphynToken.RightParen))
                .Select(x => x.ToArray());

        // Parse optional parameter list
        private static TokenListParser<MorphynToken, string[]> OptionalParameterList =>
            ParameterList.OptionalOrDefault(Array.Empty<string>());

        // Parse call arguments: (arg1, arg2, ...)
        private static TokenListParser<MorphynToken, object> CallArgument =>
            Number.Select(n => (object)n)
                .Or(String.Select(s => (object)s))
                .Or(Identifier.Select(id => (object)id));

        private static TokenListParser<MorphynToken, object[]> CallArguments =>
            CallArgument.ManyDelimitedBy(Token.EqualTo(MorphynToken.Comma))
                .Between(Token.EqualTo(MorphynToken.LeftParen), Token.EqualTo(MorphynToken.RightParen))
                .Select(x => x.ToArray());
        
        private static TokenListParser<MorphynToken, (string? target, string eventName)> EventReference =>
            (from target in Identifier
                from dot in Token.EqualTo(MorphynToken.Dot)
                from name in Identifier
                select (target: (string?)target, eventName: name))
            .Try() 
            .Or(Identifier.Select(name => (target: (string?)null, eventName: name)));

        // Parse emit action: emit eventName(args)
        private static TokenListParser<MorphynToken, MorphynAction> EmitAction =>
            from emitKeyword in Token.EqualTo(MorphynToken.Emit)
            from @ref in EventReference 
            from args in CallArguments.OptionalOrDefault(Array.Empty<object>())
            select (MorphynAction)new EmitAction 
            { 
                TargetEntityName = @ref.target, 
                EventName = @ref.eventName, 
                Arguments = args.ToList() 
            };

        // Parse check action: check expression
        private static TokenListParser<MorphynToken, MorphynAction> CheckAction =>
            from checkKeyword in Token.EqualTo(MorphynToken.Check)
            from fieldName in Identifier 
            from op in Token.EqualTo(MorphynToken.DoubleEquals)
                .Select(_ => "==")
                .Or(Token.EqualTo(MorphynToken.NotEquals).Select(_ => "!="))
                .Or(Token.EqualTo(MorphynToken.GreaterThanOrEqual).Select(_ => ">="))
                .Or(Token.EqualTo(MorphynToken.LessThanOrEqual).Select(_ => "<="))
                .Or(Token.EqualTo(MorphynToken.GreaterThan).Select(_ => ">"))
                .Or(Token.EqualTo(MorphynToken.LessThan).Select(_ => "<"))
            from value in Number
            select (MorphynAction)new CheckAction 
            { 
                Expression = $"{fieldName} {op} {value}" 
            };

        // Parse any action
        private static TokenListParser<MorphynToken, MorphynAction> ActionParser =>
            EmitAction.Or(CheckAction);

        // Parse event: on eventName(params) { actions }
        private static TokenListParser<MorphynToken, Event> EventDeclaration =>
            from onKeyword in Token.EqualTo(MorphynToken.On)
            from eventName in Identifier
            from parameters in OptionalParameterList
            from leftBrace in Token.EqualTo(MorphynToken.LeftBrace)
            from actions in ActionParser.Many()
            from rightBrace in Token.EqualTo(MorphynToken.RightBrace)
            select new Event
            {
                Name = eventName,
                Parameters = parameters.ToList(),
                Actions = actions.ToList()
            };

        // Parse entity: entity Name { fields... events... }
        private static TokenListParser<MorphynToken, Entity> EntityDeclaration =>
            from entityKeyword in Token.EqualTo(MorphynToken.Entity)
            from name in Identifier
            from leftBrace in Token.EqualTo(MorphynToken.LeftBrace)
            from fields in FieldDeclaration.Many()
            from events in EventDeclaration.Many()
            from rightBrace in Token.EqualTo(MorphynToken.RightBrace) 
            select new Entity
            {
                Name = name,
                Fields = fields.ToDictionary(f => f.Key, f => f.Value),
                Events = events.ToList()
            };
        
        // Parse multiple entities
        private static TokenListParser<MorphynToken, Entity[]> RootParser =>
            EntityDeclaration.Many().Select(x => x.ToArray());

        public static EntityData ParseFile(string input)
        {
            try
            {
                var tokens = Tokenizer.Tokenize(input);
                var result = RootParser.Parse(tokens);
                return new EntityData(result);
            }
            catch (ParseException ex)
            {
                Console.Error.WriteLine("Morphyn parse error");
                Console.Error.WriteLine($"Position: {ex.ErrorPosition}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                PrintErrorContext(input, ex.ErrorPosition);
                throw new Exception("Morphyn parsing failed. See context above.");
            }
        }

        private static void PrintErrorContext(string input, Superpower.Model.Position position)
        {
            var lines = input.Split('\n');
            int lineIndex = Math.Clamp(position.Line - 1, 0, lines.Length - 1);
            int columnIndex = Math.Clamp(position.Column - 1, 0, lines[lineIndex].Length);

            Console.Error.WriteLine("Context:");
            if (lineIndex < lines.Length)
            {
                Console.Error.WriteLine(lines[lineIndex]);
                Console.Error.WriteLine(new string(' ', columnIndex) + "^");
            }
        }
    }
}