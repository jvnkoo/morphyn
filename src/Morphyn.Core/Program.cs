using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Running; 
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Core
{

    // Main entry point for Morphyn language interpreter
    class Program
    {
        private static readonly System.Reflection.Assembly _assembly =
            System.Reflection.Assembly.GetExecutingAssembly();

        private static readonly string[] ValidExtensions = { ".mrph", ".morph", ".morphyn" };

        private static readonly Dictionary<string, string> _builtinLibs = new()
        {
            { "math", "Morphyn.Core.stdlib.math.morph" },
        };

        // CHANGED: Use array instead of List to match new MorphynRuntime.Send signature (Zero-alloc)
        private static readonly MorphynValue[] TickArgsBuffer = new MorphynValue[] { MorphynValue.FromDouble(0.0) };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                // Console.WriteLine("Usage: morphyn <filename.morphyn>");
                return;
            }

            // ── Benchmark mode ────────────────────────────────────────────
            if (BenchmarkUtils.IsBenchmarkMode(args))
            {
                Console.WriteLine("--- Launching Benchmark ---");
                BenchmarkRunner.Run<MorphynBenchmarks>();
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

                    if (entity.Events.Any(e => e.Name == "init"))
                    {
                        MorphynRuntime.Send(entity, "init");
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
                    double dtMs = currentFrameTime - lastFrameTime;
                    lastFrameTime = currentFrameTime;

                    // Update buffer without new allocations
                    TickArgsBuffer[0] = MorphynValue.FromDouble(dtMs);

                    int tickCount = tickEntities.Count;
                    for (int i = 0; i < tickCount; i++)
                    {
                        // Now passes object?[] which matches the optimized Send(Entity, string, object?[]?)
                        MorphynRuntime.Send(tickEntities[i], "tick", TickArgsBuffer);
                    }

                    MorphynRuntime.RunFullCycle(context);
                    MorphynRuntime.GarbageCollect(context);
                }
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

        static string ResolveImports(string filePath, HashSet<string> visited)
        {
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
                            string? builtinContent = TryLoadBuiltin(fileName);
                            if (builtinContent != null)
                            {
                                finalContent.Add(builtinContent);
                            }
                            else
                            {
                                var sb = new System.Text.StringBuilder();
                                sb.AppendLine($"[Error] Import file not found: {subPath} (imported from {absolutePath})");
                                sb.AppendLine($"[Suggestion] Make sure the file exists and is in the correct directory.");
                                MorphynParser.OnError(sb.ToString());
                                throw new Exception("Morphyn parsing failed. See context above.");
                            }
                        }
                        continue; 
                    }
                }
                finalContent.Add(line);
            }

            return string.Join("\n", finalContent);
        }

        private static string? TryLoadBuiltin(string importName)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(importName);

            if (_builtinLibs.TryGetValue(name, out var resourceName))
            {
                using var stream = _assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new System.IO.StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
            return null;
        }
    }

    public static class BenchmarkUtils
    {
        public static bool IsBenchmarkMode(string[] args) => args.Contains("--bench");
    }
}