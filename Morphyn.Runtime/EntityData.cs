using Morphyn.Parser;

namespace Morphyn.Runtime
{
    public class EntityData
    {
        public Dictionary<string, Entity> Entities = new();

        public Dictionary<string, int> Values = new();
    }
}