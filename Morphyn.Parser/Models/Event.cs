namespace Morphyn.Parser
{
    /// <summary>
    /// Represents an event of an entity in language. (on)
    /// </summary>
    public class Event
    {
        public required string Name { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
        public List<MorphynAction> Actions { get; set; } = new List<MorphynAction>();
    }
}