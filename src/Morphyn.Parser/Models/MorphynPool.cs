using System.Collections.Generic;
using System.Linq;

namespace Morphyn.Parser
{
    public class MorphynPool
    {
        public List<object> Values { get; set; } = new List<object>();
        
        // Only for debug
        public override string ToString()
        {
            return $"pool[{string.Join(", ", Values.Select(v => v is string ? $"\"{v}\"" : v.ToString()))}]";
        }
    }
}