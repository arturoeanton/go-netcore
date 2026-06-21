namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>The safe `string ↔ []byte` reinterpret idioms from Go 1.20+'s `unsafe`
/// builtins. goclr has no raw memory, so it cannot honour the zero-copy contract — but
/// `unsafe.String(unsafe.SliceData(b), n)` is semantically `string(b[:n])` and
/// `unsafe.Slice(unsafe.StringData(s), n)` is `[]byte(s[:n])`, which it lowers to a copy.
/// The compiler only routes the composed forms here (a bare *byte has no representation).
/// See docs/DESIGN-unsafe-pointer.md.</summary>
public static class Unsafe
{
    // unsafe.String(unsafe.SliceData(b), n): the string of the first n bytes of b.
    public static GoString String(GoSlice b, long n)
    {
        long m = System.Math.Min(n < 0 ? 0 : n, b.Len);
        var bytes = new byte[m];
        for (int i = 0; i < m; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return GoString.FromBytesOwned(bytes);
    }

    // unsafe.Slice(unsafe.StringData(s), n): the []byte of the first n bytes of s.
    public static GoSlice Slice(GoString s, long n)
    {
        var all = s.Bytes;
        long m = System.Math.Min(n < 0 ? 0 : n, all.Length);
        var data = new object?[m];
        for (int i = 0; i < m; i++) data[i] = (int)all[i];
        return new GoSlice { Data = data, Off = 0, Len = (int)m, Cap = (int)m };
    }
}
