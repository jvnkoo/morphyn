using System.Collections.Generic;

namespace Morphyn.Parser
{
    /// <summary>
    /// Represents an event of an entity in language. (on)
    /// </summary>
    public class Event
    {
        public required string Name { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
        public MorphynAction[] Actions { get; set; } = System.Array.Empty<MorphynAction>();
    }
}