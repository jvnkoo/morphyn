using System.Collections.Generic;

namespace Morphyn.Parser
{
    // Struct for pending events in the event queue, optimized for performance and minimal GC overhead.
    public readonly struct PendingEvent
    {
        public readonly Entity Target;
        public readonly string EventName;
        public readonly object?[] Args;
        public readonly int ArgCount;

        public PendingEvent(Entity target, string eventName, object?[] args, int argCount)
        {
            Target = target;
            EventName = eventName;
            Args = args;
            ArgCount = argCount;
        }
    }
}