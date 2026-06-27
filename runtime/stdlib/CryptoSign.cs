namespace GoCLR.Stdlib;

using System.Numerics;
using System.Security.Cryptography;
using GoCLR.Runtime;

// Asymmetric sign/verify for the functions golang-jwt and crypto users compile in. ECDSA and
// RSA-PKCS1v15 are now backed by the same real .NET key handles crypto/x509 already produces
// (GoEcKey/GoRsaKey), so Sign/Verify actually work. RSA-PSS and Ed25519 remain fail-closed
// (an honest error / false) — see LIMITATIONS.
public static class CryptoSign
{
    private static GoError NotSupported(string algo) =>
        new GoError(GoString.FromDotNetString("goclr: " + algo + " sign/verify is not implemented"));

    // Go's crypto.Hash enum (MD4=1, MD5=2, SHA1=3, SHA224=4, SHA256=5, SHA384=6, SHA512=7).
    private static HashAlgorithmName? HashName(ulong h) => h switch
    {
        3 => HashAlgorithmName.SHA1,
        5 => HashAlgorithmName.SHA256,
        6 => HashAlgorithmName.SHA384,
        7 => HashAlgorithmName.SHA512,
        _ => null, // 0 (raw, no DigestInfo) or an unsupported hash
    };

    // --- ecdsa.Sign(rand, priv, hash) (r, s, err) / ecdsa.Verify(pub, hash, r, s) bool ---
    // .NET signs/verifies the IEEE-P1363 fixed-width r‖s concatenation; Go exposes r and s as
    // two *big.Int, so split/recombine around the curve's field byte length.
    public static object?[] EcdsaSign(object? rand, object? priv, GoSlice hash)
    {
        if (priv is not GoEcKey k) return new object?[] { null, null, NotSupported("ECDSA") };
        try
        {
            int n = (k.Key.KeySize + 7) / 8;
            byte[] sig = k.Key.SignHash(Raw(hash));
            var r = new BigInteger(sig[..n], isUnsigned: true, isBigEndian: true);
            var s = new BigInteger(sig[n..], isUnsigned: true, isBigEndian: true);
            return new object?[] { new GoBigInt { V = r }, new GoBigInt { V = s }, null };
        }
        catch (System.Exception e) { return new object?[] { null, null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static bool EcdsaVerify(object? pub, GoSlice hash, object? r, object? s)
    {
        if (pub is not GoEcKey k) return false;
        try
        {
            int n = (k.Key.KeySize + 7) / 8;
            byte[] sig = new byte[2 * n];
            PadBE(BI(r), n).CopyTo(sig, 0);
            PadBE(BI(s), n).CopyTo(sig, n);
            return k.Key.VerifyHash(Raw(hash), sig);
        }
        catch { return false; }
    }

    // --- rsa.SignPKCS1v15 / VerifyPKCS1v15 (crypto.Hash arrives as a uint -> ulong) ---
    public static object?[] SignPKCS1v15(object? rand, object? priv, ulong hash, GoSlice hashed)
    {
        var algo = HashName(hash);
        if (algo == null || priv is not GoRsaKey k)
            return new object?[] { default(GoSlice), NotSupported("RSA-PKCS1v15 (unsupported crypto.Hash)") };
        try { return new object?[] { Bytes(k.Key.SignHash(Raw(hashed), algo.Value, RSASignaturePadding.Pkcs1)), null }; }
        catch (System.Exception e) { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object? VerifyPKCS1v15(object? pub, ulong hash, GoSlice hashed, GoSlice sig)
    {
        var algo = HashName(hash);
        if (algo == null || pub is not GoRsaKey k) return NotSupported("RSA-PKCS1v15 (unsupported crypto.Hash)");
        try
        {
            return k.Key.VerifyHash(Raw(hashed), Raw(sig), algo.Value, RSASignaturePadding.Pkcs1)
                ? null
                : new GoError(GoString.FromDotNetString("crypto/rsa: verification error"));
        }
        catch { return new GoError(GoString.FromDotNetString("crypto/rsa: verification error")); }
    }

    // --- rsa.VerifyPSS / SignPSS: PSS salt-length compatibility with Go is not yet settled,
    // so these stay fail-closed (the hash arg is still ulong so the call resolves cleanly). ---
    public static object VerifyPSS(object? pub, ulong hash, GoSlice digest, GoSlice sig, object? opts) => NotSupported("RSA-PSS");
    public static object?[] SignPSS(object? rand, object? priv, ulong hash, GoSlice digest, object? opts) =>
        new object?[] { default(GoSlice), NotSupported("RSA-PSS") };

    // crypto/ed25519 — backed by the pure RFC 8032 Ed25519 (the BCL has no primitive).
    public static bool Ed25519Verify(GoSlice pub, GoSlice message, GoSlice sig) =>
        Ed25519.VerifyRaw(Raw(pub), Raw(message), Raw(sig));

    // NewKeyFromSeed(seed) PrivateKey: 64-byte key = seed(32) || public(32).
    public static GoSlice Ed25519NewKeyFromSeed(GoSlice seed)
    {
        byte[] s = Raw(seed);
        if (s.Length != 32) throw new GoPanicException(GoString.FromDotNetString("ed25519: bad seed length: " + s.Length));
        byte[] pub = Ed25519.PublicFromSeed(s);
        var priv = new byte[64];
        System.Array.Copy(s, 0, priv, 0, 32);
        System.Array.Copy(pub, 0, priv, 32, 32);
        return Bytes(priv);
    }

    // Sign(priv, message) []byte.
    public static GoSlice Ed25519Sign(GoSlice priv, GoSlice message)
    {
        byte[] p = Raw(priv);
        if (p.Length != 64) throw new GoPanicException(GoString.FromDotNetString("ed25519: bad private key length: " + p.Length));
        return Bytes(Ed25519.SignRaw(p, Raw(message)));
    }

    // GenerateKey(rand) (PublicKey, PrivateKey, error): draw a 32-byte seed from the reader.
    public static object?[] Ed25519GenerateKey(object? rand)
    {
        var seedSlice = Bytes(new byte[32]);
        var res = Io.ReadFull(rand ?? Crypto.RandReader(), seedSlice);
        if (res.Length > 1 && res[1] != null)
            return new object?[] { default(GoSlice), default(GoSlice), res[1] };
        var priv = Raw(Ed25519NewKeyFromSeed(seedSlice));
        var pub = new byte[32];
        System.Array.Copy(priv, 32, pub, 0, 32);
        return new object?[] { Bytes(pub), Bytes(priv), null };
    }

    // (ed25519.PrivateKey).Public() crypto.PublicKey (an interface, so boxed) — the last 32 bytes.
    public static object Ed25519PrivateKey_Public(GoSlice priv) { byte[] p = Raw(priv); var pub = new byte[32]; System.Array.Copy(p, 32, pub, 0, 32); return Bytes(pub); }
    // (ed25519.PrivateKey).Seed() []byte — the leading 32 bytes.
    public static GoSlice Ed25519PrivateKey_Seed(GoSlice priv) { byte[] p = Raw(priv); var sd = new byte[32]; System.Array.Copy(p, 0, sd, 0, 32); return Bytes(sd); }
    // (ed25519.PrivateKey).Sign(rand, message, opts) (signature, error) — the crypto.Signer form.
    public static object?[] Ed25519PrivateKey_Sign(GoSlice priv, object? rand, GoSlice message, object? opts) =>
        new object?[] { Ed25519Sign(priv, message), null };

    // x509.ParsePKIXPublicKey / ParsePKCS1PublicKey (der) (key, err): goclr does not parse
    // these DER public keys (the asymmetric JWT paths that need them are unsupported), so
    // report an error rather than a bogus key.
    public static object?[] ParsePKIXPublicKey(GoSlice der) =>
        new object?[] { null, NotSupported("x509 public-key parsing") };
    public static object?[] ParsePKCS1PublicKey(GoSlice der) =>
        new object?[] { null, NotSupported("x509 public-key parsing") };

    // x509.DecryptPEMBlock(block, password) ([]byte, error): legacy encrypted PEM — unsupported.
    public static object?[] DecryptPEMBlock(object? block, GoSlice password) =>
        new object?[] { default(GoSlice), NotSupported("x509.DecryptPEMBlock") };

    // --- helpers ---
    private static BigInteger BI(object? o) => o switch
    {
        GoBigInt g => g.V,
        GoPtr p when GoPtrs.Get(p) is GoBigInt g2 => g2.V,
        _ => BigInteger.Zero,
    };
    private static byte[] PadBE(BigInteger v, int len)
    {
        byte[] b = v.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (b.Length == len) return b;
        var p = new byte[len];
        if (b.Length < len) System.Array.Copy(b, 0, p, len - b.Length, b.Length);
        else System.Array.Copy(b, b.Length - len, p, 0, len);
        return p;
    }
    private static byte[] Raw(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
}
