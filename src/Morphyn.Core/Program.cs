using System;
using System.IO;
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Core
{
    using System.Collections.Generic;
    using System.Linq;

    // Main entry point for Morphyn language interpreter
    class Program
    {
        private static readonly string[] ValidExtensions = { ".mrph", ".morph", ".morphyn" };
        private static readonly List<object?> TickArgsBuffer = new List<object?>(1) { 0.0 };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                // Console.WriteLine("Usage: morphyn <filename.morphyn>");
                return;
            }

            string path = args[0];
            string ext = Path.GetExtension(path).ToLower();

            if (!ValidExtensions.Contains(ext))
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
                string code = ResolveImports(path, new HashSet<string>());
                EntityData context = MorphynParser.ParseFile(code);

                ValidateEntities(context);

                // Console.WriteLine($"[System] Loaded {context.Entities.Count} entities.");

                foreach (var entity in context.Entities.Values)
                {
                    entity.BuildCache();
                    // Console.WriteLine($"[AST] Entity loaded: {entity.Name}");
                    // foreach (var field in entity.Fields)
                    //    Console.WriteLine($"  -> {field.Key}: {field.Value}");

                    if (entity.Events.Any(e => e.Name == "init"))
                    {
                        MorphynRuntime.Send(entity, "init");
                    }
                    else
                    {
                        // Console.WriteLine($"  [Info] {entity.Name} has no 'init' event. Static data only.");
                    }
                }

                Console.WriteLine("\n--- Starting Runtime ---");

                MorphynRuntime.RunFullCycle(context);

                Console.WriteLine("\n--- Engine Pulse Started (Press Ctrl+C to stop) ---");

                string fullPath = Path.GetFullPath(path);
                string? directory = Path.GetDirectoryName(fullPath);

                using var watcher = new FileSystemWatcher(directory ?? Environment.CurrentDirectory)
                {
                    Filter = "*.morphyn",
                    NotifyFilter = NotifyFilters.LastWrite
                };

                bool needsReload = false;
                watcher.Changed += (s, e) => needsReload = true;
                watcher.EnableRaisingEvents = true;

                // Pre-collect entities with tick handlers
                var tickEntities = new List<Entity>();
                foreach (var entity in context.Entities.Values)
                {
                    if (entity.Events.Any(e => e.Name == "tick"))
                        tickEntities.Add(entity);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                double lastFrameTime = 0;

                while (true)
                {
                    if (needsReload)
                    {
                        System.Threading.Thread.Sleep(50);
                        ReloadLogic(path, context, tickEntities);
                        needsReload = false;
                        stopwatch.Restart();
                        lastFrameTime = 0;
                    }

                    double currentFrameTime = stopwatch.Elapsed.TotalMilliseconds;
                    // Calculate fps
                    double dtMs = currentFrameTime - lastFrameTime;
                    lastFrameTime = currentFrameTime;

                    TickArgsBuffer[0] = dtMs;

                    int tickCount = tickEntities.Count;
                    for (int i = 0; i < tickCount; i++)
                    {
                        MorphynRuntime.Send(tickEntities[i], "tick", TickArgsBuffer);
                    }

                    MorphynRuntime.RunFullCycle(context);

                    MorphynRuntime.GarbageCollect(context);

                    // System.Threading.Thread.Sleep(16); 
                }

                // Console.WriteLine("--- Simulation Finished ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parser/Runtime Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void ValidateEntities(EntityData data)
        {
            foreach (var entity in data.Entities.Values)
            {
                var duplicateField = entity.Fields.Keys
                    .GroupBy(name => name)
                    .FirstOrDefault(g => g.Count() > 1);

                if (duplicateField != null)
                {
                    throw new Exception(
                        $"[Semantic Error]: Entity '{entity.Name}' has multiple fields named '{duplicateField.Key}'.");
                }

                var duplicateEvent = entity.Events
                    .GroupBy(e => e.Name)
                    .FirstOrDefault(g => g.Count() > 1);

                if (duplicateEvent != null)
                {
                    throw new Exception(
                        $"[Semantic Error]: Entity '{entity.Name}' has multiple definitions for event '{duplicateEvent.Key}'. Event names must be unique.");
                }
            }
        }

        static void ReloadLogic(string path, EntityData currentData, List<Entity> tickEntities)
        {
            try
            {
                Console.WriteLine("\n[Hot Reload] Changes detected! Processing...");
                string code = ResolveImports(path, new HashSet<string>());

                EntityData newData = MorphynParser.ParseFile(code);

                foreach (var newEntry in newData.Entities)
                {
                    string name = newEntry.Key;
                    Entity newEntity = newEntry.Value;

                    if (currentData.Entities.TryGetValue(name, out var existingEntity))
                    {
                        existingEntity.Events = newEntity.Events;
                        existingEntity.BuildCache();
                        Console.WriteLine($"[Hot Reload] Logic updated: {name}");
                    }
                    else
                    {
                        newEntity.BuildCache();
                        currentData.Entities.Add(name, newEntity);
                        Console.WriteLine($"[Hot Reload] New entity spawned: {name}");

                        if (newEntity.Events.Any(e => e.Name == "init"))
                        {
                            MorphynRuntime.Send(newEntity, "init");
                        }

                        if (newEntity.Events.Any(e => e.Name == "tick"))
                        {
                            tickEntities.Add(newEntity);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hot Reload Error]: {ex.Message}");
            }
        }

        // Resolves import statements recursively
        // filePath: Path to the file to process
        // visited: Set of already processed files (prevents circular imports)
        // Returns: Combined content of all imported files
        static string ResolveImports(string filePath, HashSet<string> visited)
        {
            // Get absolute path to ensure uniqueness in 'visited' set
            string absolutePath = Path.GetFullPath(filePath);
            if (visited.Contains(absolutePath)) return "";
            visited.Add(absolutePath);

            if (!File.Exists(absolutePath))
            {
                Console.WriteLine($"[Error] File not found: {absolutePath}");
                return "";
            }

            string content = File.ReadAllText(absolutePath);
            var lines = content.Split('\n');
            var finalContent = new List<string>(lines.Length);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("import ") && trimmed.Contains("\""))
                {
                    int firstQuote = trimmed.IndexOf('"');
                    int lastQuote = trimmed.LastIndexOf('"');

                    if (firstQuote != -1 && lastQuote > firstQuote)
                    {
                        string fileName = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

                        string? currentDir = Path.GetDirectoryName(absolutePath);
                        string subPath = Path.GetFullPath(Path.Combine(currentDir ?? "", fileName));

                        if (File.Exists(subPath))
                        {
                            finalContent.Add(ResolveImports(subPath, visited));
                        }
                        else
                        {
                            Console.WriteLine(
                                $"[Warning] Import file not found: {subPath} (imported from {absolutePath})");
                        }
                        
                        continue; 
                    }
                }
                
                finalContent.Add(line);
            }

            return string.Join("\n", finalContent);
        }
    }
}