using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Core
{
    public static class Benchmark
    {
        private static readonly object?[] TickArgsBuffer = new object?[] { 0.0 };
        private static readonly object BoxedDt = 16.6;

        private const string StressTestCode = @"
entity stress_test {
    has x: 0
    has y: 0
    has speed: 1.5
    has counter: 0
    has is_active: true

    event tick(dt) {
        check is_active
        
        counter + 1 -> counter
        x + (speed * dt) -> x
        
        check x > 100: {
            0 -> x
            y + 1 -> y
            (speed * 1.05) -> speed
        }

        check counter > 500: {
            (speed * 0.95) + 0.01 -> speed
            0 -> counter
        }
    }
}";

        public static bool IsBenchmarkMode(string[] args) => args.Contains("--bench");

        public static void Run(string[] args)
        {
            var opts = ParseOptions(args);
            PrintHeader();

            EntityData context;
            string modeName;

            try 
            {
                if (string.IsNullOrEmpty(opts.FilePath)) {
                    modeName = "Internal Stress-Test (Math & Logic)";
                    context = MorphynParser.ParseFile(StressTestCode);
                } else {
                    modeName = $"File: {Path.GetFileName(opts.FilePath)}";
                    context = MorphynParser.ParseFile(File.ReadAllText(opts.FilePath));
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"\n[Parser Error]: {ex.Message}");
                return;
            }

            var tickEntities = context.Entities.Values
                .Where(e => e.Events.Any(ev => ev.Name == "tick"))
                .ToList();

            foreach (var e in context.Entities.Values) e.BuildCache();

            Console.WriteLine($"  [Mode]       : {modeName}");
            Console.WriteLine($"  [Entities]   : {context.Entities.Count} ({tickEntities.Count} active)");
            Console.WriteLine($"  [Iterations] : {opts.Iterations:N0} pulses");
            Console.WriteLine($"  [System]     : .NET {Environment.Version} ({(IntPtr.Size == 8 ? "64-bit" : "32-bit")})");
            Console.WriteLine();

            Console.Write("  warming up...");
            for (int i = 0; i < opts.Warmup; i++) SinglePulse(context, tickEntities, BoxedDt);
            Console.WriteLine(" done\n");

            var results = new List<RunResult>();

            for (int run = 1; run <= opts.Runs; run++)
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                long startAlloc = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < opts.Iterations; i++)
                {
                    SinglePulse(context, tickEntities, BoxedDt);
                }

                sw.Stop();
                long endAlloc = GC.GetAllocatedBytesForCurrentThread();
                
                results.Add(new RunResult {
                    Run = run,
                    TotalMs = sw.Elapsed.TotalMilliseconds,
                    AllocatedBytes = endAlloc - startAlloc,
                    Iterations = opts.Iterations
                });

                var c = results.Last();
                Console.WriteLine($"  Run {run}: {c.NsPerOp,8:F1} ns/p | {c.OpsPerSec,11:N0} p/s | Alloc: {c.BytesPerOp,5:F1} B/p");
            }

            PrintSummary(results);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SinglePulse(EntityData context, List<Entity> tickEntities, object dt)
        {
            TickArgsBuffer[0] = dt;
            for (int i = 0; i < tickEntities.Count; i++)
                MorphynRuntime.Send(tickEntities[i], "tick", TickArgsBuffer);
            
            MorphynRuntime.RunFullCycle(context);
            MorphynRuntime.GarbageCollect(context);
        }

        private static void PrintSummary(List<RunResult> results)
        {
            var avgNs = results.Average(r => r.NsPerOp);
            var avgOps = results.Average(r => r.OpsPerSec);
            var avgBytes = results.Average(r => r.BytesPerOp);
            var bestNs = results.Min(r => r.NsPerOp);
            var totalMB = results.Sum(r => r.AllocatedBytes) / 1024.0 / 1024.0;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  ┌── Detailed Performance Report ──────────────────────────────┐");
            Console.ResetColor();

            Console.WriteLine($"  │ TIME & THROUGHPUT:                                          │");
            Console.WriteLine($"  │   Mean Latency    : {avgNs,10:F2} ns/pulse (tick duration)     │");
            Console.WriteLine($"  │   Best Case       : {bestNs,10:F2} ns/pulse                    │");
            Console.WriteLine($"  │   Throughput      : {avgOps,10:N0} pulses/sec                  │");
            Console.WriteLine($"  │                                                             │");

            Console.WriteLine($"  │ MEMORY & TRAFFIC:                                           │");
            Console.WriteLine($"  │   Alloc Per Pulse : {avgBytes,10:F2} bytes                        │");
            Console.WriteLine($"  │   Total Traffic   : {totalMB,10:F2} MB (all runs combined)     │");
            
            if (avgBytes < 1.0) 
                Console.WriteLine($"  │   GC Status       : ZERO-ALLOCATION (Clean)                 │");
            else
                Console.WriteLine($"  │   Boxing Pressure : {avgBytes / 24.0,10:F1} double-objects/pulse       │");
            
            Console.WriteLine($"  │                                                             │");

            Console.WriteLine($"  │ GAME SCALABILITY (Target 60 FPS / 16.6ms):                  │");
            double budget10 = (16.6 * 1_000_000 * 0.1) / avgNs;
            double budget100 = (16.6 * 1_000_000) / avgNs;
            Console.WriteLine($"  │   10% CPU Budget  : {budget10,10:N0} active entities           │");
            Console.WriteLine($"  │   100% CPU Budget : {budget100,10:N0} active entities           │");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  └─────────────────────────────────────────────────────────────┘\n");
            Console.ResetColor();
        }

        private static BenchOptions ParseOptions(string[] args)
        {
            var opts = new BenchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--file" && i + 1 < args.Length) opts.FilePath = args[i + 1];
                if (args[i] == "--iterations" && i + 1 < args.Length) int.TryParse(args[i + 1], out opts.Iterations);
                if (args[i] == "--runs" && i + 1 < args.Length) int.TryParse(args[i + 1], out opts.Runs);
            }
            return opts;
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n--- Morphyn Engine Stress-Benchmark ---");
            Console.ResetColor();
        }

        class BenchOptions { public string? FilePath; public int Iterations = 1000000; public int Runs = 3; public int Warmup = 2000; }
        class RunResult {
            public int Run;
            public double TotalMs;
            public long AllocatedBytes;
            public int Iterations;
            public double NsPerOp => (TotalMs * 1_000_000.0) / Iterations;
            public double OpsPerSec => Iterations / (TotalMs / 1000.0);
            public double BytesPerOp => (double)AllocatedBytes / Iterations;
        }
    }
}