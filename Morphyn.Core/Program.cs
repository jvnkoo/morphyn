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

                // Parse all 'has' statements into AST data
                var fields = MorphynParser.ParseFile(code);

                // Initialize a test entity with parsed data
                var entity = new Entity { Name = Path.GetFileNameWithoutExtension(path) };
                foreach (var field in fields)
                {
                    entity.Has.Add(field);
                    Console.WriteLine($"[AST] Field loaded: {field.name} = {field.value}");
                }

                // Trigger a default runtime event
                MorphynRuntime.Emit(entity, "start");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parser/Runtime Error: {ex.Message}");
            }
        }
    }
}