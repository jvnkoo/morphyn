namespace Morphyn.Parser
{
    public class Entity
    {
        public string Name; // Entity name, for example "Player"
        public Dictionary<string, object> Fields { get; set; } = new(); // Entity fields for runtime
        public List<Event> Events = new(); // Events (on)
    }
}