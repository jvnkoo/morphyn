namespace Morphyn.Parser
{
    /// <summary>
    /// Represents an entity in language.
    /// </summary>
    public class Entity
    {
        public required string Name { get; set; }
        public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
        public List<Event> Events { get; set; } = new List<Event>();
        public Dictionary<string, Event> EventCache { get; set; } = new();
        
        public void BuildCache() 
        {
            EventCache = Events.ToDictionary(e => e.Name, e => e);
        }
    }
}