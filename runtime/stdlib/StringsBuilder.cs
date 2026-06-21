namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

[GoShim("strings.Builder")]
public sealed class GoStringBuilder { public readonly StringBuilder SB = new(); }

/// <summary>Shim for strings.Builder.</summary>
public static class StringsBuilder
{
    public static object New() => new GoStringBuilder();
    private static StringBuilder S(object b) => ((GoStringBuilder)b).SB;

    public static GoString String(object b) => GoString.FromDotNetString(S(b).ToString());
    public static long Len(object b) => Encoding.UTF8.GetByteCount(S(b).ToString());
    public static long Cap(object b) => S(b).Capacity;
    public static void Reset(object b) => S(b).Clear();
    public static void Grow(object b, long n) => S(b).EnsureCapacity(S(b).Length + (int)n);

    public static object?[] WriteString(object b, GoString s) { S(b).Append(s.ToDotNetString()); return new object?[] { (long)s.Len, null }; }
    public static object? WriteByte(object b, int c) { S(b).Append((char)(byte)c); return null; }
    public static object?[] WriteRune(object b, int r) { var s = char.ConvertFromUtf32(r); S(b).Append(s); return new object?[] { (long)Encoding.UTF8.GetByteCount(s), null }; }
    public static object?[] Write(object b, GoSlice p)
    {
        var bytes = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) bytes[i] = (byte)System.Convert.ToInt32(p.Data[p.Off + i]);
        S(b).Append(Encoding.UTF8.GetString(bytes));
        return new object?[] { (long)p.Len, null };
    }
}
