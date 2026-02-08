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
        public bool IsDestroyed { get; set; } = false;
        public Dictionary<string, Event> EventCache { get; set; } = new();
        
        public void BuildCache() 
        {
            EventCache = Events.ToDictionary(e => e.Name, e => e);
        }
        
        public Entity Clone() 
        {
            var clone = new Entity 
            {
                Name = this.Name,
                IsDestroyed = false,
                Events = this.Events, 
                EventCache = this.EventCache,
                Fields = new Dictionary<string, object>()
            };

            foreach (var kvp in this.Fields) 
            {
                if (kvp.Value is MorphynPool pool)
                    clone.Fields[kvp.Key] = new MorphynPool { Values = new List<object>(pool.Values) };
                else
                    clone.Fields[kvp.Key] = kvp.Value;
            }
            return clone;
        }
    }
}