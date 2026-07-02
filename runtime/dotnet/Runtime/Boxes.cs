namespace GoCLR.Runtime;

/// <summary>Shared immutable boxes for small scalar values. Boxing into `object`
/// is pervasive in goclr's value model (interfaces, slice/map elements); reusing
/// one box per small value removes the per-element allocation. Safe because boxed
/// values are never mutated in place and interface equality is value-based.</summary>
public static class Boxes
{
    private static readonly object[] I8Cache = BuildI8();
    private static readonly object[] I4Cache = BuildI4();
    private static readonly object TrueBox = true, FalseBox = false;

    private static object[] BuildI8()
    {
        var t = new object[1152];
        for (int i = 0; i < t.Length; i++) t[i] = (long)(i - 128);
        return t;
    }

    private static object[] BuildI4()
    {
        var t = new object[1152];
        for (int i = 0; i < t.Length; i++) t[i] = i - 128;
        return t;
    }

    public static object I8(long v) => v >= -128 && v < 1024 ? I8Cache[v + 128] : v;
    public static object I4(int v) => v >= -128 && v < 1024 ? I4Cache[v + 128] : v;
    public static object Bool(bool b) => b ? TrueBox : FalseBox;

    /// <summary>byte[] → the canonical GoSlice of boxed (int) bytes, all from the
    /// shared cache (a []byte's elements are always 0..255).</summary>
    public static GoSlice ByteSlice(byte[] b)
    {
        var data = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) data[i] = I4Cache[b[i] + 128];
        return new GoSlice { Data = data, Off = 0, Len = b.Length, Cap = b.Length };
    }
}
