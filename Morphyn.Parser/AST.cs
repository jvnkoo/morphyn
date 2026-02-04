namespace Morphyn.Parser
{
    /// AST.cs
    /// Defines data structures for Morphyn
    /// After parsing, Parser creates objects of these classes
    
    public class Entity
    {
        public string Name; // Entity name, for example "Player"
        public Dictionary<string, int> Fields { get; set; } = new(); // Entity fields for runtime
        public List<Event> Events = new(); // Events (on)
    }
    
    // Event (on)
    public class Event
    {
        public string Name; // Event name, for example "damage"
        public List<string> Statements = new(); // List of properties as a text           
    }

    // Fields
    public record class MorphynField(string Name, object Value);
}