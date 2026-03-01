using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Morphyn.Parser
{
    public enum MorphynValueKind : byte { Null, Double, Bool, String, Object }

    [StructLayout(LayoutKind.Auto)]
    public struct MorphynValue
    {
        public MorphynValueKind Kind;
        public double NumVal;
        public bool BoolVal;
        public object? ObjVal;

        public static readonly MorphynValue Null = new() { Kind = MorphynValueKind.Null };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue FromDouble(double v) => new() { Kind = MorphynValueKind.Double, NumVal = v };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue FromBool(bool v) => new() { Kind = MorphynValueKind.Bool, BoolVal = v };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MorphynValue FromObject(object? v)
        {
            if (v == null) return Null;
            if (v is double d) return FromDouble(d);
            if (v is bool b) return FromBool(b);
            if (v is string) return new() { Kind = MorphynValueKind.String, ObjVal = v };
            return new() { Kind = MorphynValueKind.Object, ObjVal = v };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly object? ToObject() => Kind switch
        {
            MorphynValueKind.Null   => null,
            MorphynValueKind.Double => NumVal,
            MorphynValueKind.Bool   => BoolVal,
            MorphynValueKind.String => ObjVal,
            MorphynValueKind.Object => ObjVal,
            _                       => null
        };

        public readonly bool IsNull => Kind == MorphynValueKind.Null;
        public readonly bool IsDouble => Kind == MorphynValueKind.Double;
        public readonly bool IsBool => Kind == MorphynValueKind.Bool;
    }
}