namespace Morphyn.Parser
{
    /// <summary>
    /// Represents an event of an entity in language. (on)
    /// </summary>
    public class Event
    {
        public string Name; // Event name, for example "damage"
        public List<string> Arguments = new(); // List of arguments, for example "amount"
        public List<string> Statements = new(); // List of properties as a text           
        public List<MorphynAction> Actions = new();
    }
}