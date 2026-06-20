namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Small runtime helpers the lowering calls as externs (things that need
/// to inspect a value-type's internals, like a slice's nil backing array).</summary>
public static class Rt
{
    /// <summary>A slice is nil iff its backing array is null (Go's `s == nil`).</summary>
    public static bool SliceIsNil(GoSlice s) => s.Data == null;

    /// <summary>The nil slice value (zero value of every slice type): a GoSlice with
    /// a null backing array, so `s == nil` is true and append still works.</summary>
    public static GoSlice NilSlice() => default;
}
