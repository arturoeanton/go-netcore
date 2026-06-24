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
}
