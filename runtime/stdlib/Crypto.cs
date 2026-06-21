namespace GoCLR.Stdlib;

using System.Security.Cryptography;
using GoCLR.Runtime;

/// <summary>A hash.Hash handle: buffers written bytes and computes the digest on
/// Sum (so Sum is non-destructive, like Go).</summary>
public sealed class GoHash
{
    public readonly System.Collections.Generic.List<byte> Buf = new();
    public string Algo = "";
    public int Size, Block;
    public byte[]? Key; // for HMAC
}

/// <summary>A crypto/sha3 SHAKE handle: absorbs written bytes, then squeezes an
/// arbitrary-length output stream (Read may be called repeatedly).</summary>
public sealed class GoShake
{
    public int Bits; // 128 or 256
    public readonly System.Collections.Generic.List<byte> Input = new();
    public int Squeezed; // bytes already read from the output stream

    // SHAKE output is the deterministic first (offset+n) bytes of an infinite stream,
    // so a repeated Read recomputes that prefix and returns the fresh tail — Read calls
    // therefore form a continuous stream, matching crypto/sha3.SHAKE.Read.
    public byte[] Squeeze(int n)
    {
        int offset = Squeezed;
        byte[] full;
        if (Bits == 128)
        {
            if (!System.Security.Cryptography.Shake128.IsSupported)
                throw new GoPanicException(GoString.FromDotNetString("crypto/sha3: SHAKE128 is not available on this platform"));
            using var sh = new System.Security.Cryptography.Shake128();
            sh.AppendData(Input.ToArray());
            full = sh.GetHashAndReset(offset + n);
        }
        else
        {
            if (!System.Security.Cryptography.Shake256.IsSupported)
                throw new GoPanicException(GoString.FromDotNetString("crypto/sha3: SHAKE256 is not available on this platform"));
            using var sh = new System.Security.Cryptography.Shake256();
            sh.AppendData(Input.ToArray());
            full = sh.GetHashAndReset(offset + n);
        }
        Squeezed += n;
        var tail = new byte[n];
        System.Array.Copy(full, offset, tail, 0, n);
        return tail;
    }
}

/// <summary>Shims for crypto/sha256, sha1, sha512, md5, hmac, and crypto/rand.</summary>
public static class Crypto
{
    private static GoHash H(string algo, int size, int block) => new() { Algo = algo, Size = size, Block = block };

    // package New() constructors (return hash.Hash).
    public static object Sha256New() => H("SHA256", 32, 64);
    public static object Sha224New() => H("SHA224", 28, 64);
    public static object Sha1New() => H("SHA1", 20, 64);
    public static object Sha512New() => H("SHA512", 64, 128);
    public static object Sha384New() => H("SHA384", 48, 128);
    public static object Md5New() => H("MD5", 16, 64);

    // crypto/sha3: SHA-3 fixed-output hashes. SHA3-256/384/512 use the platform
    // implementation; SHA3-224 has no .NET built-in and reports unsupported at digest.
    public static object Sha3_224New() => H("SHA3-224", 28, 144);
    public static object Sha3_256New() => H("SHA3-256", 32, 136);
    public static object Sha3_384New() => H("SHA3-384", 48, 104);
    public static object Sha3_512New() => H("SHA3-512", 64, 72);
    public static GoSlice Sha3Sum224(GoSlice d) => Slice(Sha3Digest("SHA3-224", Bytes(d)), default);
    public static GoSlice Sha3Sum256(GoSlice d) => Slice(Sha3Digest("SHA3-256", Bytes(d)), default);
    public static GoSlice Sha3Sum384(GoSlice d) => Slice(Sha3Digest("SHA3-384", Bytes(d)), default);
    public static GoSlice Sha3Sum512(GoSlice d) => Slice(Sha3Digest("SHA3-512", Bytes(d)), default);

    private static byte[] Sha3Digest(string algo, byte[] data) => algo switch
    {
        "SHA3-256" when SHA3_256.IsSupported => SHA3_256.HashData(data),
        "SHA3-384" when SHA3_384.IsSupported => SHA3_384.HashData(data),
        "SHA3-512" when SHA3_512.IsSupported => SHA3_512.HashData(data),
        _ => throw new GoPanicException(GoString.FromDotNetString(
            "crypto/sha3: " + algo + " is not available on this platform")),
    };

    // crypto/sha3: SHAKE/cSHAKE extendable-output functions, backed by the platform
    // SHAKE. cSHAKE customization (N, S) is not exposed by the platform and is treated
    // as plain SHAKE; this is noted rather than silently wrong-by-default.
    public static object NewSHAKE128() => new GoShake { Bits = 128 };
    public static object NewSHAKE256() => new GoShake { Bits = 256 };
    public static object NewCSHAKE128(GoSlice n, GoSlice s) => new GoShake { Bits = 128 };
    public static object NewCSHAKE256(GoSlice n, GoSlice s) => new GoShake { Bits = 256 };

    public static object?[] Shake_Write(object sh, GoSlice p)
    {
        var s = (GoShake)sh;
        s.Input.AddRange(Bytes(p));
        return new object?[] { (long)p.Len, null };
    }
    public static object?[] Shake_Read(object sh, GoSlice outp)
    {
        var s = (GoShake)sh;
        byte[] got = s.Squeeze(outp.Len);
        for (int i = 0; i < got.Length; i++) outp.Data![outp.Off + i] = (int)got[i];
        return new object?[] { (long)got.Length, null };
    }
    public static void Shake_Reset(object sh) { var s = (GoShake)sh; s.Input.Clear(); s.Squeezed = 0; }
    public static long Shake_Size(object sh) => ((GoShake)sh).Bits == 128 ? 32 : 64;
    public static long Shake_BlockSize(object sh) => ((GoShake)sh).Bits == 128 ? 168 : 136;

    // SHAKE state (de)serialization, used by sha3.SHAKE.Clone: encode the absorbed
    // input and the squeeze offset; restore them.
    public static object?[] Shake_MarshalBinary(object sh)
    {
        var s = (GoShake)sh;
        var b = new System.Collections.Generic.List<byte>();
        b.Add((byte)(s.Bits == 256 ? 1 : 0));
        b.Add((byte)(s.Squeezed >> 24)); b.Add((byte)(s.Squeezed >> 16)); b.Add((byte)(s.Squeezed >> 8)); b.Add((byte)s.Squeezed);
        b.AddRange(s.Input);
        var d = new object?[b.Count];
        for (int i = 0; i < b.Count; i++) d[i] = (int)b[i];
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = b.Count, Cap = b.Count }, null };
    }
    public static object? Shake_UnmarshalBinary(object sh, GoSlice data)
    {
        var s = (GoShake)sh;
        byte[] b = Bytes(data);
        if (b.Length < 5) return new GoError("sha3: invalid SHAKE state");
        s.Bits = b[0] == 1 ? 256 : 128;
        s.Squeezed = (b[1] << 24) | (b[2] << 16) | (b[3] << 8) | b[4];
        s.Input.Clear();
        for (int i = 5; i < b.Length; i++) s.Input.Add(b[i]);
        return null;
    }

    public static object HmacNew(GoClosure newFn, GoSlice key)
    {
        var h = (GoHash)GoRuntime.InvokeArgs(newFn)!; // call the func()hash.Hash
        h.Key = Bytes(key);
        return h;
    }
    public static bool HmacEqual(GoSlice a, GoSlice b)
    {
        if (a.Len != b.Len) return false;
        int diff = 0;
        for (int i = 0; i < a.Len; i++) diff |= (byte)System.Convert.ToInt64(a.Data![a.Off + i]) ^ (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return diff == 0;
    }

    private static byte[] Bytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
    private static GoSlice Slice(byte[] b, GoSlice prefix)
    {
        int pn = prefix.Data == null ? 0 : prefix.Len;
        var d = new object?[pn + b.Length];
        for (int i = 0; i < pn; i++) d[i] = prefix.Data![prefix.Off + i];
        for (int i = 0; i < b.Length; i++) d[pn + i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    private static byte[] Digest(GoHash h)
    {
        byte[] data = h.Buf.ToArray();
        if (h.Key != null)
        {
            using HMAC hm = h.Algo switch
            {
                "SHA1" => new HMACSHA1(h.Key),
                "SHA512" => new HMACSHA512(h.Key),
                "MD5" => new HMACMD5(h.Key),
                _ => new HMACSHA256(h.Key),
            };
            return hm.ComputeHash(data);
        }
        if (h.Algo.StartsWith("SHA3-", System.StringComparison.Ordinal)) return Sha3Digest(h.Algo, data);
        using HashAlgorithm ha = h.Algo switch
        {
            "SHA1" => SHA1.Create(),
            "SHA224" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            "MD5" => MD5.Create(),
            _ => SHA256.Create(),
        };
        return ha.ComputeHash(data);
    }

    // hash.Hash methods.
    public static object?[] Hash_Write(object hh, GoSlice p) { var h = (GoHash)hh; h.Buf.AddRange(Bytes(p)); return new object?[] { (long)p.Len, null }; }
    public static GoSlice Hash_Sum(object hh, GoSlice b) => Slice(Digest((GoHash)hh), b);
    public static void Hash_Reset(object hh) => ((GoHash)hh).Buf.Clear();
    public static long Hash_Size(object hh) => ((GoHash)hh).Size;
    public static long Hash_BlockSize(object hh) => ((GoHash)hh).Block;

    // crypto/rand.Read(b) — fill b with cryptographically-random bytes.
    public static object?[] RandRead(GoSlice b)
    {
        var buf = new byte[b.Len];
        RandomNumberGenerator.Fill(buf);
        for (int i = 0; i < b.Len; i++) b.Data![b.Off + i] = (int)buf[i];
        return new object?[] { (long)b.Len, null };
    }
}
