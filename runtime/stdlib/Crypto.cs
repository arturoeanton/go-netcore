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
