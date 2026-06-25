namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An index/suffixarray.Index — holds the indexed data. The suffix-array itself is
/// not materialized: FindAllIndex delegates to the regexp engine (Go's FindAllIndex returns
/// the same sorted matches; with goclr's empty literal-prefix it takes that path too).</summary>
[GoShim("index/suffixarray.Index")]
public sealed class GoSuffixIndex { public GoSlice Data; }

public static class Suffixarray
{
    public static object New(GoSlice data) => new GoSuffixIndex { Data = data };
    public static object NewZero() => new GoSuffixIndex();

    // (*Index).Bytes() []byte: the indexed data (Go returns the live slice).
    public static GoSlice Index_Bytes(object x) => ((GoSuffixIndex)x).Data;

    // (*Index).FindAllIndex(r, n) [][]int: sorted, non-overlapping matches of r over the data.
    // For n < 0 (all matches) this equals regexp.FindAllIndex exactly. For n > 0 with a literal
    // regexp, real Go uses the suffix array and may return a different (non-successive) subset;
    // that case is not reproduced (we return the first n successive matches), as the suffix
    // array is not materialized.
    public static GoSlice Index_FindAllIndex(object x, object r, long n) =>
        Regexp.Re_FindAllIndex(r, ((GoSuffixIndex)x).Data, n);

    // (*Index).Lookup(s, n) []int: byte offsets where s occurs in the data, at most n
    // (all for n < 0). Go returns them in an unspecified (suffix-array) order; this
    // returns them in ascending positional order — for n < 0 the SET is exact (sort to
    // compare byte-for-byte), and an empty s or n == 0 yields nil, matching Go.
    public static GoSlice Lookup(object x, GoSlice s, long n)
    {
        if (n == 0 || s.Len == 0) return new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
        var hay = Bytes(((GoSuffixIndex)x).Data);
        var needle = Bytes(s);
        var offs = new System.Collections.Generic.List<object?>();
        for (int i = 0; i + needle.Length <= hay.Length; i++)
        {
            bool hit = true;
            for (int j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { hit = false; break; }
            if (hit)
            {
                offs.Add((long)i);
                if (n > 0 && offs.Count >= (int)n) break;
            }
        }
        return new GoSlice { Data = offs.ToArray(), Off = 0, Len = offs.Count, Cap = offs.Count };
    }

    private static byte[] Bytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
}
