using System.Collections.Generic;

namespace Morphyn.Parser
{
    public class EntityData
    {
        public Dictionary<string, Entity> Entities = new();
        public Dictionary<string, int> Values = new();

        public EntityData() { }

        public EntityData(IEnumerable<Entity> entities)
        {
            foreach (var e in entities)
            {
                if (!Entities.ContainsKey(e.Name))
                    Entities[e.Name] = e;
            }
        }
    }
}