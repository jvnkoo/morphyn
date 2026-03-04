using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    internal static class ObjectPools
    {
        private static readonly MorphynValue[] EmptyArgsArray = Array.Empty<MorphynValue>();

        // Pool for reusing dictionaries to avoid GC pressure during high-frequency events
        private static readonly Stack<Dictionary<string, MorphynValue>> _scopePool = new();

        // Pool for reusing object arrays for event arguments
        private static readonly Stack<MorphynValue[]> _argArrayPool = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue[] RentArgsArray(int size)
        {
            if (size == 0) return EmptyArgsArray;
            if (_argArrayPool.TryPop(out var arr))
            {
                if (arr.Length >= size) return arr;
                _argArrayPool.Push(arr);
            }
            return new MorphynValue[Math.Max(8, size)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnArgsArray(MorphynValue[] arr)
        {
            if (arr == null || arr.Length == 0 || arr == EmptyArgsArray) return;
            Array.Clear(arr, 0, arr.Length);
            _argArrayPool.Push(arr);
        }

        public static Dictionary<string, MorphynValue> RentScope(int capacity)
        {
            if (_scopePool.TryPop(out var scope))
            {
                scope.Clear();
                return scope;
            }
            return new Dictionary<string, MorphynValue>(capacity);
        }

        public static void ReturnScope(Dictionary<string, MorphynValue> scope)
        {
            if (_scopePool.Count < 128)
                _scopePool.Push(scope);
        }

        public static MorphynValue[] Empty => EmptyArgsArray;
    }
}