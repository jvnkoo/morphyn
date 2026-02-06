using System;
using System.IO;
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Core
{
    class Program
    {
        static void Main(string[] args)
        {
            // Check if file path is provided via CLI
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: morphyn <filename.morphyn>");
                return;
            }

            string path = args[0];

            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: File '{path}' not found.");
                return;
            }

            try
            {
                // Read all text from the provided file
                string code = File.ReadAllText(path);

                EntityData context = MorphynParser.ParseFile(code);
                
                Console.WriteLine($"[System] Loaded {context.Entities.Count} entities.");

                foreach (var entity in context.Entities.Values)
                {
                    Console.WriteLine($"[AST] Entity loaded: {entity.Name}");
                    
                    // Dictionary<string, int>
                    foreach (var field in entity.Fields)
                    {
                        Console.WriteLine($"  -> {field.Key}: {field.Value}");
                    }

                    // Trigger runtime vent for each entity
                    MorphynRuntime.Emit(context, entity, "start");
                    Console.WriteLine(); 
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parser/Runtime Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}