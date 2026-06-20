namespace GoCLR.Runtime;

/// <summary>
/// GoStrings holds the string operations the compiler calls into. They take and
/// return <see cref="GoString"/> by value (and primitives), so lowering never has
/// to take the address of a value-type or call instance methods on it.
/// </summary>
public static class GoStrings
{
    /// <summary>Materializes a GoString from an emitted UTF-16 literal.</summary>
    public static GoString FromLiteral(string s) => GoString.FromDotNetString(s);

    /// <summary>len(s) — byte length.</summary>
    public static long Len(GoString s) => s.Len;

    /// <summary>s[i] — the byte at index i, widened to int (Go byte is unsigned).</summary>
    public static int ByteAt(GoString s, long i) => s.ByteAt((int)i);

    /// <summary>a + b.</summary>
    public static GoString Concat(GoString a, GoString b) => GoString.Concat(a, b);

    /// <summary>a == b.</summary>
    public static bool Equal(GoString a, GoString b) => a.Equals(b);

    /// <summary>Lexicographic byte comparison: &lt;0, 0, or &gt;0.</summary>
    public static long Compare(GoString a, GoString b) => GoString.Compare(a, b);

    /// <summary>The rune that begins at byte index i (for range-over-string).</summary>
    public static int RuneAt(GoString s, long i)
    {
        var (r, _) = GoString.DecodeRune(s.Bytes.AsSpan((int)i));
        return r;
    }

    /// <summary>The byte width of the rune that begins at byte index i.</summary>
    public static long RuneSize(GoString s, long i)
    {
        var (_, size) = GoString.DecodeRune(s.Bytes.AsSpan((int)i));
        return size;
    }

    /// <summary>[]byte(s): a slice of the UTF-8 bytes (each widened to int).</summary>
    public static GoSlice ToByteSlice(GoString s)
    {
        var b = s.Bytes;
        var data = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) data[i] = (int)b[i];
        return new GoSlice { Data = data, Off = 0, Len = b.Length, Cap = b.Length };
    }

    /// <summary>[]rune(s): a slice of the decoded runes.</summary>
    public static GoSlice ToRuneSlice(GoString s)
    {
        var r = s.ToRunes();
        var data = new object?[r.Length];
        for (int i = 0; i < r.Length; i++) data[i] = r[i];
        return new GoSlice { Data = data, Off = 0, Len = r.Length, Cap = r.Length };
    }

    /// <summary>string(r): a single-rune string (invalid code points become U+FFFD).</summary>
    public static GoString FromRune(long r)
    {
        int cp = (int)r;
        if (!System.Text.Rune.IsValid(cp)) cp = 0xFFFD;
        return GoString.FromDotNetString(char.ConvertFromUtf32(cp));
    }

    /// <summary>string(b) for b []byte.</summary>
    public static GoString FromBytes(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt32(b.Data[b.Off + i]);
        return GoString.FromBytesOwned(bytes);
    }

    /// <summary>string(rs) for rs []rune.</summary>
    public static GoString FromRunes(GoSlice rs)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < rs.Len; i++)
        {
            int cp = System.Convert.ToInt32(rs.Data[rs.Off + i]);
            if (!System.Text.Rune.IsValid(cp)) cp = 0xFFFD;
            sb.Append(char.ConvertFromUtf32(cp));
        }
        return GoString.FromDotNetString(sb.ToString());
    }
}
