namespace Morphyn.Parser
{
    /// <summary>
    /// Represents an entity in language.
    /// </summary>
    public class Entity
    {
        public string Name { get; set; }
        public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
        public List<Event> Events { get; set; } = new List<Event>();
    }
}