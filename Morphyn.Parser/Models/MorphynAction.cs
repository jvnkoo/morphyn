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
    /// Represents an action that checks a condition. 
    /// </summary>
    public class CheckAction : MorphynAction
    {
        public MorphynExpression Left { get; set; }
        public string Operator { get; set; }
        public MorphynExpression Right { get; set; }
        public MorphynAction? InlineAction { get; set; }
    }
    
    public class SetAction : MorphynAction
    {
        public MorphynExpression Expression;
        public string TargetField { get; set; }
    }
    
    public class SetIndexAction : MorphynAction
    {
        public string TargetPoolName { get; set; }
        public MorphynExpression IndexExpr { get; set; }
        public MorphynExpression ValueExpr { get; set; }
    }
    
    public class BlockAction : MorphynAction
    {
        public List<MorphynAction> Actions { get; set; } = new();
    }
}