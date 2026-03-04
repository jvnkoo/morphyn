using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Morphyn.Runtime
{
    internal static class ObjectPools
    {
        private static readonly object?[] EmptyArgsArray = Array.Empty<object?>();

        // Pool for reusing dictionaries to avoid GC pressure during high-frequency events
        private static readonly Stack<Dictionary<string, object?>> _scopePool = new();

        // Pool for reusing object arrays for event arguments
        private static readonly Stack<object?[]> _argArrayPool = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object?[] RentArgsArray(int size)
        {
            if (size == 0) return EmptyArgsArray;
            if (_argArrayPool.TryPop(out var arr))
            {
                if (arr.Length >= size) return arr;
                _argArrayPool.Push(arr);
            }
            return new object?[Math.Max(8, size)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnArgsArray(object?[] arr)
        {
            if (arr == null || arr.Length == 0 || arr == EmptyArgsArray) return;
            Array.Clear(arr, 0, arr.Length);
            _argArrayPool.Push(arr);
        }

        public static Dictionary<string, object?> RentScope(int capacity)
        {
            if (_scopePool.TryPop(out var scope))
            {
                scope.Clear();
                return scope;
            }
            return new Dictionary<string, object?>(capacity);
        }

        public static void ReturnScope(Dictionary<string, object?> scope)
        {
            if (_scopePool.Count < 128)
                _scopePool.Push(scope);
        }

        public static object?[] Empty => EmptyArgsArray;
    }
}