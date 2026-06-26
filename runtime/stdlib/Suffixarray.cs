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
    // (all for n < 0). Go returns them in suffix-array order — the matching positions sorted
    // by their suffix data[pos:] lexicographically — and for n > 0 it returns the first n in
    // that order. We reproduce that order exactly (collect all matches, sort by suffix, then
    // truncate). An empty s or n == 0 yields nil, matching Go.
    public static GoSlice Lookup(object x, GoSlice s, long n)
    {
        if (n == 0 || s.Len == 0) return new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
        var hay = Bytes(((GoSuffixIndex)x).Data);
        var needle = Bytes(s);
        var pos = new System.Collections.Generic.List<int>();
        for (int i = 0; i + needle.Length <= hay.Length; i++)
        {
            bool hit = true;
            for (int j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { hit = false; break; }
            if (hit) pos.Add(i);
        }
        // Suffix-array order: sort positions by data[pos:] (a shorter suffix that is a prefix
        // of a longer one sorts first), matching Go's sorted-suffix Lookup output.
        pos.Sort((a, b) =>
        {
            int la = hay.Length - a, lb = hay.Length - b, m = la < lb ? la : lb;
            for (int k = 0; k < m; k++) { int d = hay[a + k] - hay[b + k]; if (d != 0) return d; }
            return la - lb;
        });
        int count = (n > 0 && pos.Count > (int)n) ? (int)n : pos.Count;
        var offs = new object?[count];
        for (int i = 0; i < count; i++) offs[i] = (long)pos[i];
        return new GoSlice { Data = offs, Off = 0, Len = count, Cap = count };
    }

    private static byte[] Bytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
}
