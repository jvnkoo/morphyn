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
        public List<object> Arguments { get; init; } = new();
    }

    /// <summary>
    /// Represents an action that checks a condition. 
    /// </summary>
    public class CheckAction : MorphynAction
    {
        public required string Expression { get; set; }
    }
}