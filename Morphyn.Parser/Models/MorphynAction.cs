using System.Collections.Generic;

namespace Morphyn.Parser
{
    /// <summary>
    /// Base class for all actions.
    /// </summary>
    public abstract class MorphynAction { }

    /// <summary>
    /// Represents an action that emits an event.
    /// </summary>
    public class EmitAction : MorphynAction
    {
        public string? TargetEntityName { get; init; }
        public required string EventName { get; init; }
        public List<MorphynExpression> Arguments { get; init; } = new();
    }

    /// <summary>
    /// Represents an action that emits an event synchronously and assigns the result to a field.
    /// Nested sync calls are not allowed to prevent recursion.
    /// </summary>
    public class EmitWithReturnAction : MorphynAction
    {
        public string? TargetEntityName { get; init; }
        public required string EventName { get; init; }
        public List<MorphynExpression> Arguments { get; init; } = new();
        public required string TargetField { get; init; }
    }

    /// <summary>
    /// Represents an action that checks a condition. 
    /// </summary>
    public class CheckAction : MorphynAction
    {
        public required MorphynExpression Condition { get; set; }
        public MorphynExpression Left { get; set; } = null!;
        public string Operator { get; set; } = null!;
        public MorphynExpression Right { get; set; } = null!;
        public MorphynAction? InlineAction { get; set; }
    }

    public class SetAction : MorphynAction
    {
        public MorphynExpression Expression = null!;
        public required string TargetField { get; set; }
    }

    public class SetIndexAction : MorphynAction
    {
        public required string TargetPoolName { get; set; }
        public required MorphynExpression IndexExpr { get; set; }
        public required MorphynExpression ValueExpr { get; set; }
    }

    public class BlockAction : MorphynAction
    {
        public List<MorphynAction> Actions { get; set; } = new();
    }
}