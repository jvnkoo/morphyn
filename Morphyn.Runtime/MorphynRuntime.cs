using Morphyn.Parser;

namespace Morphyn.Runtime
{
    /// <summary>
    /// The AST execution is implemented here.
    /// </summary>
    public static class MorphynRuntime
    {
        // Executes the event for the entity
        public static void Emit(Entity entity, string eventName, int value = 0)
        {
            var ev = entity.Events.FirstOrDefault(e => e.Name == eventName);

            if (ev != null)
            {
                foreach (var stmt in ev.Statements)
                {
                    Console.WriteLine($"Executing: {stmt} with value: {value}");
                }
            }
        }
    }
}