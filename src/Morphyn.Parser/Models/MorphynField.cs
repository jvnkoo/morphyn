using System.Collections.Generic;

namespace Morphyn.Parser
{
    /// <summary>
    /// Represents a field in a Morphyn entity.
    /// Fields are used to store data in an entity.
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="Value"></param>
    public record class MorphynField(string Name, object Value);
}