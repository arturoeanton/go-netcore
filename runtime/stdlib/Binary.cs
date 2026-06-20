namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A binary.ByteOrder handle (little- or big-endian).</summary>
public sealed class GoByteOrder { public bool Big; }

/// <summary>Shim for Go's <c>encoding/binary</c> LittleEndian/BigEndian fixed-width
/// Uint/Put helpers over a []byte (GoSlice).</summary>
public static class Binary
{
    public static object LittleEndian() => new GoByteOrder { Big = false };
    public static object BigEndian() => new GoByteOrder { Big = true };

    private static byte Get(GoSlice b, int i) => (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
    private static void Set(GoSlice b, int i, byte v) => b.Data![b.Off + i] = (int)v;

    public static uint Uint16(object o, GoSlice b)
    {
        var e = (GoByteOrder)o;
        return (uint)(e.Big ? (Get(b, 0) << 8) | Get(b, 1) : Get(b, 0) | (Get(b, 1) << 8));
    }
    public static uint Uint32(object o, GoSlice b)
    {
        var e = (GoByteOrder)o; uint r = 0;
        for (int i = 0; i < 4; i++) r |= (uint)Get(b, i) << (e.Big ? (3 - i) * 8 : i * 8);
        return r;
    }
    public static ulong Uint64(object o, GoSlice b)
    {
        var e = (GoByteOrder)o; ulong r = 0;
        for (int i = 0; i < 8; i++) r |= (ulong)Get(b, i) << (e.Big ? (7 - i) * 8 : i * 8);
        return r;
    }
    public static void PutUint16(object o, GoSlice b, uint v)
    {
        var e = (GoByteOrder)o;
        if (e.Big) { Set(b, 0, (byte)(v >> 8)); Set(b, 1, (byte)v); } else { Set(b, 0, (byte)v); Set(b, 1, (byte)(v >> 8)); }
    }
    public static void PutUint32(object o, GoSlice b, uint v)
    {
        var e = (GoByteOrder)o;
        for (int i = 0; i < 4; i++) Set(b, i, (byte)(v >> (e.Big ? (3 - i) * 8 : i * 8)));
    }
    public static void PutUint64(object o, GoSlice b, ulong v)
    {
        var e = (GoByteOrder)o;
        for (int i = 0; i < 8; i++) Set(b, i, (byte)(v >> (e.Big ? (7 - i) * 8 : i * 8)));
    }
}
