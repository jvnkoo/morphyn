using System.Collections.Generic;

namespace Morphyn.Parser
{
    public record PendingEvent(Entity Target, string EventName, List<object?> Args);
}