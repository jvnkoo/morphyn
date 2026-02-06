using System.Collections.Generic;

namespace Morphyn.Parser
{
    public class EntityData
    {
        public Dictionary<string, Entity> Entities { get; set; } = new Dictionary<string, Entity>();

        public EntityData(IEnumerable<Entity> entities)
        {
            foreach (var entity in entities)
            {
                Entities[entity.Name] = entity;
            }
        }
    }
}