namespace GoCLR.Stdlib;

using System.Numerics;
using GoCLR.Runtime;

/// <summary>A *big.Int handle (mutable, like Go's receiver-as-destination).</summary>
public sealed class GoBigInt { public BigInteger V; }

/// <summary>Shim for a subset of Go's <c>math/big</c> (Int). Methods return the
/// receiver (the destination), matching Go's chaining.</summary>
public static class Big
{
    public static object NewInt(long x) => new GoBigInt { V = x };
    public static object IntZero() => new GoBigInt { V = 0 };
    private static BigInteger V(object o) => ((GoBigInt)o).V;

    public static object Int_Add(object z, object x, object y) { ((GoBigInt)z).V = V(x) + V(y); return z; }
    public static object Int_Sub(object z, object x, object y) { ((GoBigInt)z).V = V(x) - V(y); return z; }
    public static object Int_Mul(object z, object x, object y) { ((GoBigInt)z).V = V(x) * V(y); return z; }
    public static object Int_Div(object z, object x, object y) { ((GoBigInt)z).V = BigInteger.Divide(V(x), V(y)); return z; }
    public static object Int_Mod(object z, object x, object y) { var r = V(x) % V(y); if (r.Sign < 0) r += BigInteger.Abs(V(y)); ((GoBigInt)z).V = r; return z; }
    public static object Int_Neg(object z, object x) { ((GoBigInt)z).V = -V(x); return z; }
    public static object Int_Abs(object z, object x) { ((GoBigInt)z).V = BigInteger.Abs(V(x)); return z; }
    public static object Int_Exp(object z, object x, object y, object m)
    {
        BigInteger r = m == null ? BigInteger.Pow(V(x), (int)V(y)) : BigInteger.ModPow(V(x), V(y), V(m));
        ((GoBigInt)z).V = r; return z;
    }
    public static object Int_Set(object z, object x) { ((GoBigInt)z).V = V(x); return z; }
    public static long Int_Cmp(object z, object y) => V(z).CompareTo(V(y));
    public static long Int_Sign(object z) => V(z).Sign;
    public static long Int_Int64(object z) => (long)V(z);
    public static GoString Int_String(object z) => GoString.FromDotNetString(V(z).ToString());
    public static object?[] Int_SetString(object z, GoString s, long base_)
    {
        try
        {
            string str = s.ToDotNetString();
            BigInteger v = base_ == 16 ? ParseBase(str, 16) : base_ == 2 ? ParseBase(str, 2) : BigInteger.Parse(str);
            ((GoBigInt)z).V = v;
            return new object?[] { z, true };
        }
        catch { return new object?[] { null, false }; }
    }
    private static BigInteger ParseBase(string s, int b)
    {
        bool neg = s.StartsWith("-"); if (neg) s = s.Substring(1);
        BigInteger r = 0;
        foreach (char c in s) { int d = c <= '9' ? c - '0' : char.ToLowerInvariant(c) - 'a' + 10; r = r * b + d; }
        return neg ? -r : r;
    }
}
