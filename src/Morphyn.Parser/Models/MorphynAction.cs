using System.Collections.Generic;

namespace Morphyn.Parser
{
    /// <summary>
    /// Base class for all actions.
    /// </summary>
    public abstract class MorphynAction { }

    public class EmitAction : MorphynAction
    {
        public string? TargetEntityName { get; init; }
        public required string EventName { get; init; }
        public List<MorphynExpression> Arguments { get; init; } = new();
    }

    public class EmitWithReturnAction : MorphynAction
    {
        public string? TargetEntityName { get; init; }
        public required string EventName { get; init; }
        public List<MorphynExpression> Arguments { get; init; } = new();
        public required string TargetField { get; init; }
    }

    public class EmitWithReturnIndexAction : MorphynAction
    {
        public string? TargetEntityName { get; init; }
        public required string EventName { get; init; }
        public List<MorphynExpression> Arguments { get; init; } = new();
        public required string TargetPoolName { get; init; }
        public required MorphynExpression IndexExpr { get; init; }
    }

    /// <summary>
    /// when TargetEntity.eventName : localHandler
    /// when TargetEntity.eventName : localHandler(arg)
    /// HandlerArgs are evaluated against the subscriber entity at fire time.
    /// Null = no args forwarded to handler.
    /// </summary>
    public class WhenAction : MorphynAction
    {
        public required string TargetEntityName { get; init; }
        public required string TargetEventName { get; init; }
        public required string HandlerEventName { get; init; }
        public List<MorphynExpression>? HandlerArgs { get; init; }
    }

    /// <summary>
    /// unwhen TargetEntity.eventName : localHandler
    /// unwhen TargetEntity.eventName : localHandler(arg)
    /// </summary>
    public class UnwhenAction : MorphynAction
    {
        public required string TargetEntityName { get; init; }
        public required string TargetEventName { get; init; }
        public required string HandlerEventName { get; init; }
        public List<MorphynExpression>? HandlerArgs { get; init; }
    }

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