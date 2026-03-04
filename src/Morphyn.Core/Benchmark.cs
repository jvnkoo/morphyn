using BenchmarkDotNet.Attributes;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Morphyn.Parser;
using Morphyn.Runtime;

namespace Morphyn.Core
{
    // [MemoryDiagnoser] is the most important part—it tracks allocations.
    [MemoryDiagnoser]
    [HideColumns("Job", "Error", "StdDev", "Median")] // Keeps the output clean
    public class MorphynBenchmarks
    {
        private EntityData _context;
        private Entity _testEntity;
        private static readonly MorphynValue[] TickArgsBuffer = new MorphynValue[] { MorphynValue.FromDouble(16.6) };

        // Test Data
        private const string MorphynCode = @"
        entity stress_test {
            has x: 0
            has speed: 1.5
            event tick(dt) {
                x + (speed * dt) -> x
                check x > 100: { 0 -> x }
            }
        }";

        // Native Comparison Fields
        private double _nativeX = 0;
        private double _nativeSpeed = 1.5;

        [GlobalSetup]
        public void Setup()
        {
            // Initialize the engine once before all tests
            _context = MorphynParser.ParseFile(MorphynCode);
            _testEntity = _context.Entities.Values.First();
            _testEntity.BuildCache();
        }

        [Benchmark(Baseline = true, Description = "Native C# Logic")]
        public void NativeCSharp()
        {
            // The baseline: what C# does in ~0.1 nanoseconds
            double dt = 16.6;
            _nativeX += (_nativeSpeed * dt);
            if (_nativeX > 100) _nativeX = 0;
        }

        [Benchmark(Description = "Morphyn Single Tick")]
        public void MorphynTick()
        {
            // Measures the execution of interpreter logic
            MorphynRuntime.Send(_testEntity, "tick", TickArgsBuffer);
            MorphynRuntime.RunFullCycle(_context);
        }

        [Benchmark(Description = "Morphyn + Garbage Collect")]
        public void MorphynWithGC()
        {
            // Measures the impact of internal GC cycle
            MorphynRuntime.Send(_testEntity, "tick", TickArgsBuffer);
            MorphynRuntime.RunFullCycle(_context);
            MorphynRuntime.GarbageCollect(_context);
        }
    }
}