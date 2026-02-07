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
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: morphyn <filename.morphyn>");
                return;
            }

            string path = args[0];
            string[] validExtensions = { ".mrph", ".morph", ".morphyn" };
            string ext = Path.GetExtension(path).ToLower();

            if (!validExtensions.Contains(ext))
            {
                Console.WriteLine($"[Error]: Running file with non-standard extension '{ext}'.");
                return;
            }

            if (!File.Exists(path))
            {
                Console.WriteLine($"[Error]: File '{path}' not found.");
                return;
            }

            try
            {
                string code = File.ReadAllText(path);
                EntityData context = MorphynParser.ParseFile(code);
                
                Console.WriteLine($"[System] Loaded {context.Entities.Count} entities.");

                foreach (var entity in context.Entities.Values)
                {
                    Console.WriteLine($"[AST] Entity loaded: {entity.Name}");
                    foreach (var field in entity.Fields)
                        Console.WriteLine($"  -> {field.Key}: {field.Value}");

                    if (entity.Events.Any(e => e.Name == "init"))
                    {
                        MorphynRuntime.Send(entity, "init");
                    }
                    else 
                    {
                        Console.WriteLine($"  [Info] {entity.Name} has no 'init' event. Static data only.");
                    }
                }

                Console.WriteLine("\n--- Starting Runtime ---");

                MorphynRuntime.RunFullCycle(context);
                
                Console.WriteLine("\n--- Engine Pulse Started (Press Ctrl+C to stop) ---");

                DateTime lastTime = DateTime.Now;

                while (true)
                {
                    DateTime currentTime = DateTime.Now;
                    // Calculate fps
                    int dtMs = (int)(currentTime - lastTime).TotalMilliseconds;
                    lastTime = currentTime;

                    foreach (var entity in context.Entities.Values)
                    {
                        if (entity.Events.Any(e => e.Name == "tick"))
                        {
                            MorphynRuntime.Send(entity, "tick", new List<object>() { dtMs });
                        }
                    }
                    
                    MorphynRuntime.RunFullCycle(context);
                    
                    System.Threading.Thread.Sleep(16);
                }

                Console.WriteLine("--- Simulation Finished ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parser/Runtime Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}