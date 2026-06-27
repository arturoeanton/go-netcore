namespace GoCLR.Stdlib;

using System.Numerics;
using GoCLR.Runtime;

/// <summary>A pure RFC 8032 Ed25519 implementation (the .NET BCL has no public Ed25519).
/// Backs crypto/ed25519's NewKeyFromSeed/Sign/Verify/GenerateKey; field and scalar
/// arithmetic use BigInteger (correctness over speed), SHA-512 from the BCL. Deterministic,
/// so Sign output and the derived public key are byte-exact with `go run`.</summary>
public static class Ed25519
{
    private static readonly BigInteger P = (BigInteger.One << 255) - 19;
    private static readonly BigInteger L = (BigInteger.One << 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    private static readonly BigInteger D = Mod(-121665 * Inv(121666));
    private static readonly BigInteger I = ModPow(2, (P - 1) / 4, P); // sqrt(-1)
    private static readonly BigInteger By = Mod(4 * Inv(5));
    private static readonly BigInteger Bx = RecoverX(By, 0);

    private static BigInteger Mod(BigInteger x) { x %= P; return x < 0 ? x + P : x; }
    private static BigInteger Inv(BigInteger x) => ModPow(x, P - 2, P);
    private static BigInteger ModPow(BigInteger b, BigInteger e, BigInteger m) => BigInteger.ModPow(Mod2(b, m), e, m);
    private static BigInteger Mod2(BigInteger x, BigInteger m) { x %= m; return x < 0 ? x + m : x; }

    private static BigInteger RecoverX(BigInteger y, int sign)
    {
        BigInteger y2 = Mod(y * y);
        BigInteger xx = Mod((y2 - 1) * Inv(Mod(D * y2 + 1)));
        BigInteger x = ModPow(xx, (P + 3) / 8, P);
        if (Mod(x * x - xx) != 0) x = Mod(x * I);
        if ((int)(x & 1) != sign) x = Mod(-x);
        return x;
    }

    // Affine points on edwards25519 (-x^2 + y^2 = 1 + d x^2 y^2).
    private struct Pt { public BigInteger X, Y; }
    private static readonly Pt Base = new() { X = Bx, Y = By };

    private static Pt Add(Pt p, Pt q)
    {
        BigInteger xy = Mod(D * p.X * q.X * p.Y * q.Y);
        BigInteger x3 = Mod((p.X * q.Y + q.X * p.Y) * Inv(Mod(1 + xy)));
        BigInteger y3 = Mod((p.Y * q.Y + p.X * q.X) * Inv(Mod(1 - xy)));
        return new Pt { X = x3, Y = y3 };
    }

    private static Pt ScalarMul(Pt p, BigInteger e)
    {
        var r = new Pt { X = 0, Y = 1 }; // identity
        while (e > 0)
        {
            if ((e & 1) == 1) r = Add(r, p);
            p = Add(p, p);
            e >>= 1;
        }
        return r;
    }

    private static byte[] Encode(Pt p)
    {
        var b = p.Y.ToByteArray(isUnsigned: true, isBigEndian: false);
        var outb = new byte[32];
        System.Array.Copy(b, outb, System.Math.Min(b.Length, 32));
        outb[31] |= (byte)((int)(p.X & 1) << 7);
        return outb;
    }

    private static bool Decode(byte[] s, out Pt p)
    {
        p = default;
        if (s.Length != 32) return false;
        int sign = s[31] >> 7;
        var yb = (byte[])s.Clone();
        yb[31] &= 0x7f;
        BigInteger y = new BigInteger(yb, isUnsigned: true, isBigEndian: false);
        if (y >= P) return false;
        BigInteger x = RecoverX(y, sign);
        p = new Pt { X = x, Y = y };
        return true;
    }

    private static BigInteger LE(byte[] b, int off, int len)
    {
        var t = new byte[len + 1];
        System.Array.Copy(b, off, t, 0, len); // trailing 0 keeps it non-negative
        return new BigInteger(t);
    }

    private static byte[] Sha512(params byte[][] parts)
    {
        using var h = System.Security.Cryptography.SHA512.Create();
        foreach (var p in parts) h.TransformBlock(p, 0, p.Length, null, 0);
        h.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
        return h.Hash!;
    }

    private static byte[] ClampScalar(byte[] h)
    {
        var a = new byte[32];
        System.Array.Copy(h, a, 32);
        a[0] &= 248; a[31] &= 127; a[31] |= 64;
        return a;
    }

    private static byte[] Le32(BigInteger v)
    {
        var b = v.ToByteArray(isUnsigned: true, isBigEndian: false);
        var outb = new byte[32];
        System.Array.Copy(b, outb, System.Math.Min(b.Length, 32));
        return outb;
    }

    // ---- public surface (operates on raw byte arrays) ----
    internal static byte[] PublicFromSeed(byte[] seed)
    {
        byte[] h = Sha512(seed);
        BigInteger a = new BigInteger(ClampScalar(h), isUnsigned: true, isBigEndian: false);
        return Encode(ScalarMul(Base, a));
    }

    internal static byte[] SignRaw(byte[] priv, byte[] msg)
    {
        byte[] seed = priv[..32];
        byte[] pub = priv[32..64];
        byte[] h = Sha512(seed);
        BigInteger a = new BigInteger(ClampScalar(h), isUnsigned: true, isBigEndian: false);
        byte[] prefix = h[32..64];
        BigInteger r = Mod2(LE(Sha512(prefix, msg), 0, 64), L);
        byte[] R = Encode(ScalarMul(Base, r));
        BigInteger k = Mod2(LE(Sha512(R, pub, msg), 0, 64), L);
        BigInteger s = Mod2(r + k * a, L);
        var sig = new byte[64];
        System.Array.Copy(R, 0, sig, 0, 32);
        System.Array.Copy(Le32(s), 0, sig, 32, 32);
        return sig;
    }

    internal static bool VerifyRaw(byte[] pub, byte[] msg, byte[] sig)
    {
        if (sig.Length != 64 || pub.Length != 32) return false;
        byte[] Renc = sig[..32];
        BigInteger s = LE(sig, 32, 32);
        if (s >= L) return false;
        if (!Decode(pub, out var A)) return false;
        BigInteger k = Mod2(LE(Sha512(Renc, pub, msg), 0, 64), L);
        // [s]B == R + [k]A  <=>  R == [s]B - [k]A
        Pt sb = ScalarMul(Base, s);
        Pt ka = ScalarMul(A, k);
        Pt negKa = new Pt { X = Mod(-ka.X), Y = ka.Y };
        Pt rp = Add(sb, negKa);
        return Encode(rp).AsSpan().SequenceEqual(Renc);
    }
}
