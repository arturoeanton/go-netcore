namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for crypto/subtle (constant-time comparisons).</summary>
public static class Subtle
{
    public static long ConstantTimeCompare(GoSlice x, GoSlice y)
    {
        if (x.Len != y.Len) return 0;
        int v = 0;
        for (int i = 0; i < x.Len; i++) v |= (byte)System.Convert.ToInt64(x.Data![x.Off + i]) ^ (byte)System.Convert.ToInt64(y.Data![y.Off + i]);
        return v == 0 ? 1 : 0;
    }
    // ConstantTimeByteEq takes uint8 and ConstantTimeEq takes int32 in Go; both lower to a
    // 32-bit CLR value, so the params must be Int32 (a (long, long) signature would not bind
    // at the call site — MissingMethodException). The other functions take Go `int` (Int64).
    public static long ConstantTimeByteEq(int a, int b) => (byte)a == (byte)b ? 1 : 0;
    public static long ConstantTimeEq(int a, int b) => a == b ? 1 : 0;
    public static long ConstantTimeSelect(long v, long a, long b) => v == 1 ? a : b;

    // subtle.ConstantTimeLessOrEq(x, y) int: 1 if x <= y, else 0 (x, y in [0, 2**31-1]).
    public static long ConstantTimeLessOrEq(long x, long y)
    {
        int x32 = (int)x, y32 = (int)y;
        return ((x32 - y32 - 1) >> 31) & 1;
    }

    // subtle.ConstantTimeCopy(v, x, y): if v == 1, copy y into x; if v == 0, leave x. Panics on length mismatch.
    public static void ConstantTimeCopy(long v, GoSlice x, GoSlice y)
    {
        if (x.Len != y.Len) throw new GoPanicException(GoString.FromDotNetString("subtle: slices have different lengths"));
        byte xmask = (byte)(v - 1);
        byte ymask = (byte)(~(v - 1));
        for (int i = 0; i < x.Len; i++)
        {
            byte xi = (byte)System.Convert.ToInt64(x.Data![x.Off + i]);
            byte yi = (byte)System.Convert.ToInt64(y.Data![y.Off + i]);
            x.Data![x.Off + i] = (int)((xi & xmask) | (yi & ymask));
        }
    }

    // subtle.WithDataIndependentTiming(f): runs f (DIT is a no-op fast path off arm64).
    public static void WithDataIndependentTiming(GoClosure f) => GoRuntime.Invoke(f);

    // subtle.XORBytes(dst, x, y) int: dst[i] = x[i] ^ y[i] for i < min(len(x), len(y)).
    public static long XORBytes(GoSlice dst, GoSlice x, GoSlice y)
    {
        int n = System.Math.Min(x.Len, y.Len);
        for (int i = 0; i < n; i++)
            dst.Data![dst.Off + i] = (int)((byte)System.Convert.ToInt64(x.Data![x.Off + i]) ^ (byte)System.Convert.ToInt64(y.Data![y.Off + i]));
        return n;
    }
}
