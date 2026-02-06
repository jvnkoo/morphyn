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
        public string EventName { get; set; }
        public List<object> Arguments { get; set; } = new List<object>();
    }

    /// <summary>
    /// Represents an action that checks a condition. 
    /// </summary>
    public class CheckAction : MorphynAction
    {
        public string Expression { get; set; }
    }
}