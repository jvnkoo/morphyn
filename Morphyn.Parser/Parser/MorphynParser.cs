using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Parsers;

namespace Morphyn.Parser
{
    using System.Globalization;

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

        // Terms are the lowest level of the expression.
        // They include numbers, identifiers, and subexpressions in parentheses.
        // Subexpressions have higher priority than numbers and identifiers.
        private static TokenListParser<MorphynToken, MorphynExpression> Term =>
            Token.EqualTo(MorphynToken.Double) 
                .Select(t => (MorphynExpression)new LiteralExpression(double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture)))
                .Or(Token.EqualTo(MorphynToken.Number)
                    .Select(t => (MorphynExpression)new LiteralExpression(double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture))))
                .Or(Token.EqualTo(MorphynToken.String) 
                    .Select(t => (MorphynExpression)new LiteralExpression(t.ToStringValue().Trim('"'))))
                .Or(Token.EqualTo(MorphynToken.Identifier)
                    .Select(t => (MorphynExpression)new VariableExpression(t.ToStringValue())))
                .Or(Parse.Ref(() => Expression).Between(Token.EqualTo(MorphynToken.LeftParen),
                    Token.EqualTo(MorphynToken.RightParen)));

        // Parser precedence for multiplication and division (and modulo).
        // These operators have higher precedence than addition and subtraction.
        private static TokenListParser<MorphynToken, MorphynExpression> Factor =>
            Parse.Chain(
                Token.EqualTo(MorphynToken.Asterisk).Or(Token.EqualTo(MorphynToken.Slash))
                    .Or(Token.EqualTo(MorphynToken.Percent)),
                Term,
                (op, left, right) => (MorphynExpression)new BinaryExpression(op.ToStringValue(), left, right)
            );

        // Parser precedence for addition and subtraction.
        // These operators have lower precedence than multiplication and division.
        private static TokenListParser<MorphynToken, MorphynExpression> Expression =>
            Parse.Chain(
                Token.EqualTo(MorphynToken.Plus).Or(Token.EqualTo(MorphynToken.Minus)),
                Factor,
                (op, left, right) => (MorphynExpression)new BinaryExpression(op.ToStringValue(), left, right)
            );
        
        /// <summary>
        /// An arrow "->" is used in flow actions to indicate that the expression on the left should be assigned to the field on the right.
        /// For example, "entity.health -> player.health" sets the health of the entity to the health of the player.
        /// </summary>
        private static TokenListParser<MorphynToken, MorphynAction> FlowAction =>
            from expr in Expression
            from arrow in Token.EqualTo(MorphynToken.Arrow)
            from target in Identifier
            select (MorphynAction)new SetAction { Expression = expr, TargetField = target };

        /// <summary>
        /// Flow actions consist of an expression followed by an arrow and an identifier.
        /// The expression is evaluated and the result is assigned to the field indicated by the identifier.
        /// </summary>
        private static TokenListParser<MorphynToken, object> LiteralValue =>
            Token.EqualTo(MorphynToken.Double)
                .Select(t => (object)double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture))
                .Or(Token.EqualTo(MorphynToken.Number).Select(t =>
                    (object)double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture)))
                .Or(Token.EqualTo(MorphynToken.String).Select(t => (object)t.ToStringValue().Trim('"')))
                .Or(Token.EqualTo(MorphynToken.True).Select(_ => (object)true))
                .Or(Token.EqualTo(MorphynToken.False).Select(_ => (object)false));

        // Parse field declaration: identifier : value
        // Maybe make "has" optional
        private static TokenListParser<MorphynToken, KeyValuePair<string, object>> FieldDeclaration =>
            (from hasKeyWord in Token.EqualTo(MorphynToken.Has)
                from name in Identifier
                from colon in Token.EqualTo(MorphynToken.Colon)
                from value in LiteralValue
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
        private static TokenListParser<MorphynToken, MorphynExpression> CallArgument =>
            Expression; // emit log("HP is:", hp + 10, armor / 2)

        private static TokenListParser<MorphynToken, MorphynExpression[]> CallArguments =>
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
            from args in CallArguments.OptionalOrDefault(Array.Empty<MorphynExpression>())
            select (MorphynAction)new EmitAction
            {
                TargetEntityName = @ref.target,
                EventName = @ref.eventName,
                Arguments = args.ToList()
            };

        // Parse check action: check expression
        private static TokenListParser<MorphynToken, MorphynAction> CheckAction =>
            from checkKeyword in Token.EqualTo(MorphynToken.Check)
            from leftExpr in Expression
            from op in Token.EqualTo(MorphynToken.DoubleEquals)
                .Select(_ => "==")
                .Or(Token.EqualTo(MorphynToken.NotEquals).Select(_ => "!="))
                .Or(Token.EqualTo(MorphynToken.GreaterThanOrEqual).Select(_ => ">="))
                .Or(Token.EqualTo(MorphynToken.LessThanOrEqual).Select(_ => "<="))
                .Or(Token.EqualTo(MorphynToken.GreaterThan).Select(_ => ">"))
                .Or(Token.EqualTo(MorphynToken.LessThan).Select(_ => "<"))
            from rightExpr in Expression
            select (MorphynAction)new CheckAction 
            { 
                Left = leftExpr, 
                Operator = op, 
                Right = rightExpr 
            };

        // Parse any action
        private static TokenListParser<MorphynToken, MorphynAction> ActionParser =>
            EmitAction.Try()
                .Or(CheckAction.Try())
                .Or(FlowAction);

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