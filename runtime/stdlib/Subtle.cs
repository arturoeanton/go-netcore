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
    public static long ConstantTimeByteEq(long a, long b) => (byte)a == (byte)b ? 1 : 0;
    public static long ConstantTimeEq(long a, long b) => a == b ? 1 : 0;
    public static long ConstantTimeSelect(long v, long a, long b) => v == 1 ? a : b;

    // subtle.XORBytes(dst, x, y) int: dst[i] = x[i] ^ y[i] for i < min(len(x), len(y)).
    public static long XORBytes(GoSlice dst, GoSlice x, GoSlice y)
    {
        int n = System.Math.Min(x.Len, y.Len);
        for (int i = 0; i < n; i++)
            dst.Data![dst.Off + i] = (int)((byte)System.Convert.ToInt64(x.Data![x.Off + i]) ^ (byte)System.Convert.ToInt64(y.Data![y.Off + i]));
        return n;
    }
}
