/**
 * \file Program.cs
 * \brief Morphyn Language Runtime Entry Point
 * \defgroup core Core System
 * @{
 */

using System;
using System.IO;
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Core
{
    using System.Collections.Generic;
    using System.Linq;

    /**
     * \class Program
     * \brief Main entry point for Morphyn language interpreter
     *
     * \page getting_started Getting Started with Morphyn
     *
     * \section usage Usage
     *
     * Run a Morphyn program:
     * \code{.sh}
     * morphyn program.morphyn
     * \endcode
     *
     * \section file_extensions Supported File Extensions
     *
     * Morphyn recognizes the following file extensions:
     * - `.mrph`
     * - `.morph`
     * - `.morphyn`
     *
     * \section features Runtime Features
     *
     * \subsection hot_reload Hot Reload
     *
     * The runtime automatically watches for file changes and reloads entity logic
     * without restarting the program or losing state.
     *
     * \subsection tick_system Tick System
     *
     * Entities with a `tick` event handler receive delta time updates every frame:
     *
     * \code{.morphyn}
     * entity Player {
     *   on tick(dt) {
     *     # dt = milliseconds since last frame
     *     emit log("Frame time:", dt)
     *   }
     * }
     * \endcode
     *
     * \subsection init_event Init Event
     *
     * Entities with an `init` event are automatically initialized on load:
     *
     * \code{.morphyn}
     * entity Player {
     *   has hp: 100
     *
     *   on init {
     *     emit log("Player spawned with", hp, "HP")
     *   }
     * }
     * \endcode
     */
    class Program
    {
        private static readonly string[] ValidExtensions = { ".mrph", ".morph", ".morphyn" };
        private static readonly List<object> TickArgsBuffer = new List<object>(1) { 0.0 };

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

                Console.WriteLine("--- Simulation Finished ---");
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

        /**
          * \brief Resolves import statements recursively
          * \param filePath Path to the file to process
          * \param visited Set of already processed files (prevents circular imports)
          * \return Combined content of all imported files
          *
          * \page imports Import System
          *
          * \section import_syntax Import Syntax
          *
          * Import other Morphyn files:
          *
          * \code{.morphyn}
          * import "enemies.morphyn";
          * import "core/weapons.morphyn";
          * import "../shared/items.morphyn";
          *
          * entity Player {
          * # Can use entities from imported files
          * }
          * \endcode
          *
          * \section import_rules Import Rules
          *
          * - Imports are resolved relative to the importing file
          * - Circular imports are automatically prevented
          * - Import statements must end with semicolon
          * - Missing import files generate warnings but don't stop execution
          */
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

                // Improved import parsing to support subdirectories and relative paths
                if (trimmed.StartsWith("import ") && trimmed.EndsWith(";"))
                {
                    int firstQuote = trimmed.IndexOf('"');
                    int lastQuote = trimmed.LastIndexOf('"');

                    if (firstQuote != -1 && lastQuote > firstQuote)
                    {
                        string fileName = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

                        // Resolve path relative to the directory of the current file
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
                    }
                }
                else
                {
                    finalContent.Add(line);
                }
            }

            return string.Join("\n", finalContent);
        }
    }
}
/** @} */ // end of core group