using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Parsers;

namespace Morphyn.Parser
{
    using System.Globalization;
    using Superpower.Model;

    public static partial class MorphynParser
    {
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
        
        private static TokenListParser<MorphynToken, MorphynExpression> IndexAccess =>
            (from name in Identifier
                from dot in Token.EqualTo(MorphynToken.Dot)
                from member in Identifier 
                from index in Expression.Between(
                    Token.EqualTo(MorphynToken.LeftBracket), 
                    Token.EqualTo(MorphynToken.RightBracket))
                select (MorphynExpression)new IndexAccessExpression { 
                    TargetName = name, 
                    IndexExpr = index 
                }).Try();

        private static TokenListParser<MorphynToken, MorphynExpression> PropertyAccess =>
            (from name in Identifier
                from dot in Token.EqualTo(MorphynToken.Dot)
                from prop in Identifier
                select (MorphynExpression)new PoolPropertyExpression { 
                    TargetName = name, 
                    Property = prop 
                }).Try();

        // Terms are the lowest level of the expression.
        // They include numbers, identifiers, and subexpressions in parentheses.
        // Subexpressions have higher priority than numbers and identifiers.
        private static TokenListParser<MorphynToken, MorphynExpression> Term =>
            Token.EqualTo(MorphynToken.Double).Select(t => (MorphynExpression)new LiteralExpression(double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture)))
                .Or(Token.EqualTo(MorphynToken.Number).Select(t => (MorphynExpression)new LiteralExpression(double.Parse(t.ToStringValue(), CultureInfo.InvariantCulture))))
                .Or(Token.EqualTo(MorphynToken.String).Select(t => (MorphynExpression)new LiteralExpression(t.ToStringValue().Trim('"'))))
                .Or(Token.EqualTo(MorphynToken.True).Select(_ => (MorphynExpression)new LiteralExpression(true)))
                .Or(Token.EqualTo(MorphynToken.False).Select(_ => (MorphynExpression)new LiteralExpression(false)))
                .Or(IndexAccess)   
                .Or(PropertyAccess) 
                .Or(Identifier.Select(id => (MorphynExpression)new VariableExpression(id))) 
                .Or(Parse.Ref(() => Expression).Between(Token.EqualTo(MorphynToken.LeftParen), Token.EqualTo(MorphynToken.RightParen)));
        
        // Parser precedence for multiplication and division (and modulo).
        // These operators have higher precedence than addition and subtraction.
        private static TokenListParser<MorphynToken, MorphynExpression> Factor =>
            Parse.Chain(
                Token.EqualTo(MorphynToken.Asterisk).Or(Token.EqualTo(MorphynToken.Slash)).Or(Token.EqualTo(MorphynToken.Percent)),
                Term,
                (op, left, right) => (MorphynExpression)new BinaryExpression(op.ToStringValue(), left, right));

        // Parser precedence for addition and subtraction.
        // These operators have lower precedence than multiplication and division.
        private static TokenListParser<MorphynToken, MorphynExpression> Expression =>
            Parse.Chain(Token.EqualTo(MorphynToken.Or), AndParser, 
                (op, left, right) => (MorphynExpression)new BinaryLogicExpression(left, "or", right));

        /// <summary>
        /// An arrow "->" is used in flow actions to indicate that the expression on the left should be assigned to the field on the right.
        /// For example, "entity.health -> player.health" sets the health of the entity to the health of the player.
        /// </summary>
        private static TokenListParser<MorphynToken, MorphynAction> FlowAction =>
            (from valExpr in Expression
                from arrow in Token.EqualTo(MorphynToken.Arrow)
                from poolName in Identifier
                from dot in Token.EqualTo(MorphynToken.Dot)
                from member in Token.EqualTo(MorphynToken.Identifier).Where(t => t.ToStringValue() == "at")
                from indexExpr in Expression.Between(Token.EqualTo(MorphynToken.LeftBracket), Token.EqualTo(MorphynToken.RightBracket))
                select (MorphynAction)new SetIndexAction { 
                    TargetPoolName = poolName, 
                    IndexExpr = indexExpr, 
                    ValueExpr = valExpr 
                }).Try()
            .Or(from expr in Expression
                from arrow in Token.EqualTo(MorphynToken.Arrow)
                from target in Identifier
                select (MorphynAction)new SetAction { Expression = expr, TargetField = target });

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

        private static TokenListParser<MorphynToken, MorphynPool> PoolValues =>
            from poolKeyword in Token.EqualTo(MorphynToken.Pool)
            from values in LiteralValue.ManyDelimitedBy(Token.EqualTo(MorphynToken.Comma))
                .Between(Token.EqualTo(MorphynToken.LeftBracket), Token.EqualTo(MorphynToken.RightBracket))
            select new MorphynPool { Values = values.ToList() };


        // Parse field declaration: identifier : value
        // Maybe make "has" optional
        private static TokenListParser<MorphynToken, KeyValuePair<string, object>> FieldDeclaration =>
            (from hasKeyWord in Token.EqualTo(MorphynToken.Has)
                from name in Identifier
                from colon in Token.EqualTo(MorphynToken.Colon)
                from value in PoolValues.Cast<MorphynToken, MorphynPool, object>().Try()
                    .Or(LiteralValue)
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

        private static TokenListParser<MorphynToken, MorphynExpression> ArithExpression =>
            Parse.Chain(
                Token.EqualTo(MorphynToken.Plus).Or(Token.EqualTo(MorphynToken.Minus)),
                Factor,
                (op, left, right) => (MorphynExpression)new BinaryExpression(op.ToStringValue(), left, right));

        private static TokenListParser<MorphynToken, MorphynExpression> Comparison =>
            Parse.Chain(
                Token.EqualTo(MorphynToken.DoubleEquals).Or(Token.EqualTo(MorphynToken.NotEquals))
                    .Or(Token.EqualTo(MorphynToken.GreaterThan)).Or(Token.EqualTo(MorphynToken.LessThan))
                    .Or(Token.EqualTo(MorphynToken.GreaterThanOrEqual)).Or(Token.EqualTo(MorphynToken.LessThanOrEqual)),
                ArithExpression, 
                (op, left, right) => (MorphynExpression)new BinaryExpression(op.ToStringValue(), left, right));

        private static TokenListParser<MorphynToken, MorphynExpression> NotParser =>
            (from op in Token.EqualTo(MorphynToken.Not)
                from expr in Comparison
                select (MorphynExpression)new UnaryLogicExpression("not", expr))
            .Or(Comparison); 

        private static TokenListParser<MorphynToken, MorphynExpression> AndParser =>
            Parse.Chain(Token.EqualTo(MorphynToken.And), NotParser, 
                (op, left, right) => (MorphynExpression)new BinaryLogicExpression(left, "and", right));
        
        private static TokenListParser<MorphynToken, MorphynAction> SimpleActionParser =>
            EmitAction.Try()
                .Or(FlowAction.Try());
        
        private static TokenListParser<MorphynToken, MorphynAction> CheckAction =>
            from checkKeyword in Token.EqualTo(MorphynToken.Check)
            from condition in Expression 
            from colon in Token.EqualTo(MorphynToken.Colon)
            from action in Parse.Ref(() => ActionParser) 
            select (MorphynAction)new CheckAction
            {
                Condition = condition,
                InlineAction = action
            };
        
        private static TokenListParser<MorphynToken, MorphynAction> BlockActionParser =>
            from leftBrace in Token.EqualTo(MorphynToken.LeftBrace)
            from actions in Parse.Ref(() => ActionParser).Many()
            from rightBrace in Token.EqualTo(MorphynToken.RightBrace)
            select (MorphynAction)new BlockAction { Actions = actions.ToList() };

        // Parse any action
        private static TokenListParser<MorphynToken, MorphynAction> ActionParser =>
            BlockActionParser.Try()
                .Or(CheckAction.Try())
                .Or(SimpleActionParser);

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
            from members in (
                FieldDeclaration.Select(f => (object)f)
                    .Or(EventDeclaration.Select(e => (object)e))
            ).Many()
            from rightBrace in Token.EqualTo(MorphynToken.RightBrace)
            select new Entity
            {
                Name = name,
                Fields = members.OfType<KeyValuePair<string, object>>()
                    .GroupBy(f => f.Key).Any(g => g.Count() > 1)
                    ? throw new Exception($"[Semantic Error]: Entity '{name}' has duplicate fields.")
                    : members.OfType<KeyValuePair<string, object>>().ToDictionary(f => f.Key, f => f.Value),

                Events = members.OfType<Event>()
                    .GroupBy(e => e.Name).Any(g => g.Count() > 1)
                    ? throw new Exception($"[Semantic Error]: Entity '{name}' has duplicate events.")
                    : members.OfType<Event>().ToList()
            };

        // Parse multiple entities
        private static TokenListParser<MorphynToken, Entity[]> RootParser =>
            EntityDeclaration.Many().Select(entities =>
            {
                var duplicate = entities.GroupBy(e => e.Name).FirstOrDefault(g => g.Count() > 1);
                if (duplicate != null)
                    throw new Exception($"[Semantic Error]: Duplicate entity definition: '{duplicate.Key}'");
                return entities.ToArray();
            });
    }
}