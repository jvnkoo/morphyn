namespace Morphyn.Parser
{
    // Event (on)
    public class Event
    {
        public string Name; // Event name, for example "damage"
        public List<string> Statements = new(); // List of properties as a text           
    }
}