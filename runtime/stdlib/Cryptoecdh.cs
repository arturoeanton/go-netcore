namespace GoCLR.Stdlib;

using System;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using GoCLR.Runtime;

// crypto/ecdh: ECDH over the NIST P-256/384/521 curves and X25519. The three
// package types are opaque runtime handles:
//   - GoEcdhCurve  is one of the four curve singletons (identity-compared, as Go's
//     ecdh.Curve values are).
//   - GoEcdhPrivateKey / GoEcdhPublicKey hold the key material in Go's ecdh.*.Bytes()
//     encoding: a NIST scalar is big-endian fixed-length, a NIST public key is the
//     uncompressed SEC1 point 0x04||X||Y, and X25519 keys are 32-byte little-endian
//     per RFC 7748. Keeping the bytes canonical makes Bytes()/Equal()/round-trips
//     byte-identical to the standard library without carrying a live CLR key object.

public sealed class GoEcdhCurve
{
    public string Name = "";       // "P-256" | "P-384" | "P-521" | "X25519"
    public bool IsX25519 => Name == "X25519";
    public int Size;               // scalar / coordinate byte length
    public ECCurve Named;          // valid for the NIST curves only
    public override string ToString() => Name;
}

public sealed class GoEcdhPrivateKey
{
    public GoEcdhCurve Curve = null!;
    public byte[] Priv = Array.Empty<byte>();  // scalar bytes (Go Bytes() form)
    public byte[] Pub = Array.Empty<byte>();    // matching public key bytes
}

public sealed class GoEcdhPublicKey
{
    public GoEcdhCurve Curve = null!;
    public byte[] Pub = Array.Empty<byte>();    // public key bytes (Go Bytes() form)
}

public static class Cryptoecdh
{
    private static readonly GoEcdhCurve P256c = new() { Name = "P-256", Size = 32, Named = ECCurve.NamedCurves.nistP256 };
    private static readonly GoEcdhCurve P384c = new() { Name = "P-384", Size = 48, Named = ECCurve.NamedCurves.nistP384 };
    private static readonly GoEcdhCurve P521c = new() { Name = "P-521", Size = 66, Named = ECCurve.NamedCurves.nistP521 };
    private static readonly GoEcdhCurve X25519c = new() { Name = "X25519", Size = 32 };

    public static object P256() => P256c;
    public static object P384() => P384c;
    public static object P521() => P521c;
    public static object X25519() => X25519c;

    // The ecdh NIST curve matching an elliptic-curve name ("P-256" …), for the
    // (*ecdsa.PublicKey).ECDH / (*ecdsa.PrivateKey).ECDH conversions. null if unknown.
    internal static GoEcdhCurve? NistCurveByName(string name) => name switch
    {
        "P-256" => P256c, "P-384" => P384c, "P-521" => P521c, _ => null,
    };

    // --- ecdh.Curve methods ---

    public static object?[] Curve_GenerateKey(object c, object? rand)
    {
        var cv = (GoEcdhCurve)c;
        try
        {
            if (cv.IsX25519)
            {
                var scalar = RandomNumberGenerator.GetBytes(32);
                ClampX25519(scalar);
                var pub = X25519ScalarMult(scalar, BasePoint9());
                return new object?[] { new GoEcdhPrivateKey { Curve = cv, Priv = scalar, Pub = pub }, null };
            }
            using var k = ECDiffieHellman.Create(cv.Named);
            var p = k.ExportParameters(true);
            return new object?[] { NistPrivFromParams(cv, p), null };
        }
        catch (Exception e) { return new object?[] { null, Err(e) }; }
    }

    public static object?[] Curve_NewPrivateKey(object c, GoSlice b)
    {
        var cv = (GoEcdhCurve)c;
        byte[] raw = Raw(b);
        try
        {
            if (raw.Length != cv.Size)
                return new object?[] { null, Err(new Exception("crypto/ecdh: invalid private key size")) };
            if (cv.IsX25519)
            {
                var scalar = (byte[])raw.Clone();
                var pub = X25519ScalarMult(scalar, BasePoint9());
                return new object?[] { new GoEcdhPrivateKey { Curve = cv, Priv = scalar, Pub = pub }, null };
            }
            using var k = ECDiffieHellman.Create();
            k.ImportECPrivateKey(Sec1(cv, raw), out _);
            var p = k.ExportParameters(true);
            return new object?[] { NistPrivFromParams(cv, p), null };
        }
        catch (Exception e) { return new object?[] { null, Err(e) }; }
    }

    public static object?[] Curve_NewPublicKey(object c, GoSlice b)
    {
        var cv = (GoEcdhCurve)c;
        byte[] raw = Raw(b);
        try
        {
            if (cv.IsX25519)
            {
                if (raw.Length != 32) return new object?[] { null, Err(new Exception("crypto/ecdh: invalid public key")) };
                return new object?[] { new GoEcdhPublicKey { Curve = cv, Pub = (byte[])raw.Clone() }, null };
            }
            if (raw.Length != 1 + 2 * cv.Size || raw[0] != 0x04)
                return new object?[] { null, Err(new Exception("crypto/ecdh: invalid public key")) };
            return new object?[] { new GoEcdhPublicKey { Curve = cv, Pub = (byte[])raw.Clone() }, null };
        }
        catch (Exception e) { return new object?[] { null, Err(e) }; }
    }

    // --- ecdh.PrivateKey methods ---

    public static GoSlice PrivateKey_Bytes(object k) => Bytes(((GoEcdhPrivateKey)k).Priv);
    public static object PrivateKey_Curve(object k) => ((GoEcdhPrivateKey)k).Curve;
    public static object PrivateKey_PublicKey(object k)
    {
        var pk = (GoEcdhPrivateKey)k;
        return new GoEcdhPublicKey { Curve = pk.Curve, Pub = pk.Pub };
    }
    public static bool PrivateKey_Equal(object k, object? x)
    {
        var a = (GoEcdhPrivateKey)k;
        return x is GoEcdhPrivateKey b && a.Curve == b.Curve && ConstEq(a.Priv, b.Priv);
    }
    public static object?[] PrivateKey_ECDH(object k, object pub)
    {
        var pk = (GoEcdhPrivateKey)k;
        var pb = (GoEcdhPublicKey)pub;
        try
        {
            if (pk.Curve != pb.Curve)
                return new object?[] { null, Err(new Exception("crypto/ecdh: private key and public key curves do not match")) };
            if (pk.Curve.IsX25519)
            {
                var shared = X25519ScalarMult(pk.Priv, pb.Pub);
                if (IsAllZero(shared))
                    return new object?[] { null, Err(new Exception("crypto/ecdh: bad X25519 remote ECDH input point")) };
                return new object?[] { Bytes(shared), null };
            }
            using var priv = ECDiffieHellman.Create();
            priv.ImportECPrivateKey(Sec1(pk.Curve, pk.Priv), out _);
            using var peer = ECDiffieHellman.Create(NistPointParams(pk.Curve, pb.Pub));
            byte[] secret = priv.DeriveRawSecretAgreement(peer.PublicKey);
            return new object?[] { Bytes(secret), null };
        }
        catch (Exception e) { return new object?[] { null, Err(e) }; }
    }

    // --- ecdh.PublicKey methods ---

    public static GoSlice PublicKey_Bytes(object k) => Bytes(((GoEcdhPublicKey)k).Pub);
    public static object PublicKey_Curve(object k) => ((GoEcdhPublicKey)k).Curve;
    public static bool PublicKey_Equal(object k, object? x)
    {
        var a = (GoEcdhPublicKey)k;
        return x is GoEcdhPublicKey b && a.Curve == b.Curve && ConstEq(a.Pub, b.Pub);
    }

    // --- NIST helpers ---

    private static GoEcdhPrivateKey NistPrivFromParams(GoEcdhCurve cv, ECParameters p)
    {
        byte[] scalar = FixedLen(p.D!, cv.Size);
        byte[] pub = new byte[1 + 2 * cv.Size];
        pub[0] = 0x04;
        Array.Copy(FixedLen(p.Q.X!, cv.Size), 0, pub, 1, cv.Size);
        Array.Copy(FixedLen(p.Q.Y!, cv.Size), 0, pub, 1 + cv.Size, cv.Size);
        return new GoEcdhPrivateKey { Curve = cv, Priv = scalar, Pub = pub };
    }

    private static ECParameters NistPointParams(GoEcdhCurve cv, byte[] pub)
    {
        var x = new byte[cv.Size];
        var y = new byte[cv.Size];
        Array.Copy(pub, 1, x, 0, cv.Size);
        Array.Copy(pub, 1 + cv.Size, y, 0, cv.Size);
        return new ECParameters { Curve = cv.Named, Q = new ECPoint { X = x, Y = y } };
    }

    // Sec1 builds a minimal RFC 5915 ECPrivateKey DER (version, scalar, [0] namedCurve)
    // so .NET's ImportECPrivateKey derives the public point from the scalar for us.
    private static byte[] Sec1(GoEcdhCurve cv, byte[] scalar)
    {
        var w = new AsnWriter(AsnEncodingRules.DER);
        using (w.PushSequence())
        {
            w.WriteInteger(1);
            w.WriteOctetString(scalar);
            using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                w.WriteObjectIdentifier(CurveOid(cv));
        }
        return w.Encode();
    }

    private static string CurveOid(GoEcdhCurve cv) => cv.Name switch
    {
        "P-256" => "1.2.840.10045.3.1.7",
        "P-384" => "1.3.132.0.34",
        "P-521" => "1.3.132.0.35",
        _ => throw new Exception("crypto/ecdh: no OID for " + cv.Name),
    };

    private static byte[] FixedLen(byte[] b, int n)
    {
        if (b.Length == n) return b;
        var o = new byte[n];
        if (b.Length < n) Array.Copy(b, 0, o, n - b.Length, b.Length);
        else Array.Copy(b, b.Length - n, o, 0, n); // trim any leading zero pad
        return o;
    }

    // --- X25519 (RFC 7748) over GF(2^255 - 19), Montgomery ladder ---

    private static readonly BigInteger P25519 = (BigInteger.One << 255) - 19;
    private static readonly BigInteger A24 = 121665;

    private static byte[] BasePoint9() { var b = new byte[32]; b[0] = 9; return b; }

    private static void ClampX25519(byte[] s)
    {
        s[0] &= 248;
        s[31] &= 127;
        s[31] |= 64;
    }

    private static byte[] X25519ScalarMult(byte[] scalarIn, byte[] uIn)
    {
        var scalar = (byte[])scalarIn.Clone();
        ClampX25519(scalar);
        BigInteger k = LeToBig(scalar);
        BigInteger x1 = LeToBig(uIn) % P25519;

        BigInteger x2 = 1, z2 = 0, x3 = x1, z3 = 1;
        int swap = 0;
        for (int t = 254; t >= 0; t--)
        {
            int kt = (int)((k >> t) & 1);
            swap ^= kt;
            CSwap(swap, ref x2, ref x3);
            CSwap(swap, ref z2, ref z3);
            swap = kt;

            BigInteger a = Mod(x2 + z2);
            BigInteger aa = Mod(a * a);
            BigInteger b = Mod(x2 - z2);
            BigInteger bb = Mod(b * b);
            BigInteger e = Mod(aa - bb);
            BigInteger c = Mod(x3 + z3);
            BigInteger d = Mod(x3 - z3);
            BigInteger da = Mod(d * a);
            BigInteger cb = Mod(c * b);
            x3 = Mod((da + cb));
            x3 = Mod(x3 * x3);
            z3 = Mod((da - cb));
            z3 = Mod(x1 * Mod(z3 * z3));
            x2 = Mod(aa * bb);
            z2 = Mod(e * (aa + Mod(A24 * e)));
        }
        CSwap(swap, ref x2, ref x3);
        CSwap(swap, ref z2, ref z3);

        BigInteger res = Mod(x2 * ModInverse(z2, P25519));
        return BigToLe(res, 32);
    }

    private static BigInteger Mod(BigInteger x) { x %= P25519; if (x < 0) x += P25519; return x; }
    private static void CSwap(int swap, ref BigInteger a, ref BigInteger b)
    {
        if (swap == 1) { var t = a; a = b; b = t; }
    }
    private static BigInteger ModInverse(BigInteger a, BigInteger m) => BigInteger.ModPow(Mod(a), m - 2, m);

    private static BigInteger LeToBig(byte[] le)
    {
        var b = new byte[le.Length + 1];
        Array.Copy(le, b, le.Length); // little-endian, extra 0 keeps it positive
        return new BigInteger(b);
    }
    private static byte[] BigToLe(BigInteger v, int n)
    {
        var full = v.ToByteArray(); // little-endian, possibly with sign byte
        var o = new byte[n];
        int count = n < full.Length ? n : full.Length;
        Array.Copy(full, 0, o, 0, count);
        return o;
    }

    // --- shared helpers ---

    private static bool IsAllZero(byte[] b) { foreach (var x in b) if (x != 0) return false; return true; }
    private static bool ConstEq(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int d = 0;
        for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
        return d == 0;
    }
    private static byte[] Raw(GoSlice s)
    {
        var o = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) o[i] = (byte)Convert.ToInt64(s.Data![s.Off + i]);
        return o;
    }
    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = Boxes.I4(b[i]);
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static GoError Err(Exception e) => new(GoString.FromDotNetString(e.Message));
}
