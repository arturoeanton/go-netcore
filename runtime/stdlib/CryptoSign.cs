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

    // --- rsa.SignPSS / VerifyPSS: a from-scratch EMSA-PSS (PKCS#1 v2.1) over the raw RSA
    // primitive. The .NET BCL only offers PSS with salt-length == hash-length, so it cannot
    // honour Go's PSSOptions.SaltLength (Auto = max, EqualsHash = -1, or an explicit count);
    // doing the padding by hand (MGF1 + the EM layout of RFC 8017 §9.1) and the modular
    // exponentiation with BigInteger reproduces Go's behaviour for every salt-length mode. The
    // salt is random, so signatures are not byte-reproducible across runs (Go's aren't either),
    // but a sign->verify round-trip and verification of any Go-mode signature match exactly.
    public static object?[] SignPSS(object? rand, object? priv, ulong hash, GoSlice digest, GoPtr? opts)
    {
        long saltOpt = 0; // PSSSaltLengthAuto
        ulong h = hash;
        ReadPssOpts(opts, ref saltOpt, ref h);
        var hf = PssHash(h);
        if (hf == null || priv is not GoRsaKey k || k.PublicOnly)
            return new object?[] { default(GoSlice), NotSupported("RSA-PSS (unsupported crypto.Hash or non-private key)") };
        int hLen = hf.Value.Item1;
        byte[] mHash = Raw(digest);
        if (mHash.Length != hLen)
            return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("crypto/rsa: input must be hashed with given hash function")) };
        try
        {
            var pp = k.Key.ExportParameters(true);
            var n = new BigInteger(pp.Modulus!, isUnsigned: true, isBigEndian: true);
            var d = new BigInteger(pp.D!, isUnsigned: true, isBigEndian: true);
            int modBits = (int)n.GetBitLength();
            int emBits = modBits - 1;
            int emLen = (emBits + 7) / 8;
            int kLen = pp.Modulus!.Length;
            // Resolve the salt length per Go's signPSS: Auto -> the largest that fits.
            int sLen = saltOpt == -1 ? hLen : (saltOpt == 0 ? emLen - hLen - 2 : (int)saltOpt);
            if (sLen < 0 || emLen < hLen + sLen + 2)
                return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("crypto/rsa: key size too small for PSS signature")) };
            byte[] salt = new byte[sLen];
            if (sLen > 0) RandomNumberGenerator.Fill(salt);
            byte[] em = EmsaPssEncode(mHash, salt, emBits, emLen, hLen, hf.Value.Item2);
            var m = new BigInteger(em, isUnsigned: true, isBigEndian: true);
            var s = BigInteger.ModPow(m, d, n);
            return new object?[] { Bytes(I2OSP(s, kLen)), null };
        }
        catch (System.Exception e) { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object? VerifyPSS(object? pub, ulong hash, GoSlice digest, GoSlice sig, GoPtr? opts)
    {
        long saltOpt = 0;
        ulong h = hash;
        ReadPssOpts(opts, ref saltOpt, ref h);
        var hf = PssHash(h);
        if (hf == null || pub is not GoRsaKey k)
            return NotSupported("RSA-PSS (unsupported crypto.Hash)");
        int hLen = hf.Value.Item1;
        byte[] mHash = Raw(digest);
        if (mHash.Length != hLen) return Verr();
        try
        {
            var pp = k.Key.ExportParameters(false);
            var n = new BigInteger(pp.Modulus!, isUnsigned: true, isBigEndian: true);
            var e = new BigInteger(pp.Exponent!, isUnsigned: true, isBigEndian: true);
            int modBits = (int)n.GetBitLength();
            int emBits = modBits - 1;
            int emLen = (emBits + 7) / 8;
            int kLen = pp.Modulus!.Length;
            byte[] sigb = Raw(sig);
            if (sigb.Length != kLen) return Verr();
            var m = new BigInteger(sigb, isUnsigned: true, isBigEndian: true);
            if (m >= n) return Verr();
            var s = BigInteger.ModPow(m, e, n);
            byte[] em = I2OSP(s, emLen);
            return EmsaPssVerify(mHash, em, emBits, emLen, hLen, saltOpt, hf.Value.Item2) ? null : Verr();
        }
        catch { return Verr(); }
    }

    private static GoError Verr() => new GoError(GoString.FromDotNetString("crypto/rsa: verification error"));

    // (hLen, hashFunc) for a crypto.Hash; null for an unsupported one.
    private static (int, System.Func<byte[], byte[]>)? PssHash(ulong h) => h switch
    {
        3 => (20, (System.Func<byte[], byte[]>)(b => SHA1.HashData(b))),
        5 => (32, (System.Func<byte[], byte[]>)(b => SHA256.HashData(b))),
        6 => (48, (System.Func<byte[], byte[]>)(b => SHA384.HashData(b))),
        7 => (64, (System.Func<byte[], byte[]>)(b => SHA512.HashData(b))),
        _ => null,
    };

    // Read SaltLength / Hash out of a *rsa.PSSOptions (a boxed CLR struct behind the GoPtr).
    private static void ReadPssOpts(GoPtr? opts, ref long saltLen, ref ulong hash)
    {
        if (opts == null) return;
        object? v;
        try { v = GoPtrs.Get(opts); } catch { return; }
        if (v == null) return;
        foreach (var f in v.GetType().GetFields())
        {
            string name = f.Name.ToLowerInvariant();
            if (name.Contains("salt")) { try { saltLen = System.Convert.ToInt64(f.GetValue(v)); } catch { } }
            else if (name.Contains("hash")) { try { ulong hv = System.Convert.ToUInt64(f.GetValue(v)); if (hv != 0) hash = hv; } catch { } }
        }
    }

    // EMSA-PSS-ENCODE (RFC 8017 §9.1.1) -> the emLen-byte encoded message EM.
    private static byte[] EmsaPssEncode(byte[] mHash, byte[] salt, int emBits, int emLen, int hLen, System.Func<byte[], byte[]> fn)
    {
        int sLen = salt.Length;
        byte[] mp = new byte[8 + hLen + sLen];
        System.Array.Copy(mHash, 0, mp, 8, hLen);
        System.Array.Copy(salt, 0, mp, 8 + hLen, sLen);
        byte[] hh = fn(mp);
        int dbLen = emLen - hLen - 1;
        byte[] db = new byte[dbLen];
        db[dbLen - sLen - 1] = 0x01;
        System.Array.Copy(salt, 0, db, dbLen - sLen, sLen);
        byte[] dbMask = Mgf1(hh, dbLen, fn);
        for (int i = 0; i < dbLen; i++) db[i] ^= dbMask[i];
        db[0] &= (byte)(0xFF >> (8 * emLen - emBits));
        byte[] em = new byte[emLen];
        System.Array.Copy(db, 0, em, 0, dbLen);
        System.Array.Copy(hh, 0, em, dbLen, hLen);
        em[emLen - 1] = 0xBC;
        return em;
    }

    // EMSA-PSS-VERIFY (RFC 8017 §9.1.2). saltOpt is Go's mode: 0=Auto, -1=EqualsHash, n>0=fixed.
    private static bool EmsaPssVerify(byte[] mHash, byte[] em, int emBits, int emLen, int hLen, long saltOpt, System.Func<byte[], byte[]> fn)
    {
        if (em.Length != emLen || em[emLen - 1] != 0xBC) return false;
        int dbLen = emLen - hLen - 1;
        if (dbLen < 0) return false;
        byte[] db = new byte[dbLen];
        System.Array.Copy(em, 0, db, 0, dbLen);
        byte[] hh = new byte[hLen];
        System.Array.Copy(em, dbLen, hh, 0, hLen);
        byte bitMask = (byte)(0xFF >> (8 * emLen - emBits));
        if ((em[0] & (byte)~bitMask) != 0) return false;
        byte[] dbMask = Mgf1(hh, dbLen, fn);
        for (int i = 0; i < dbLen; i++) db[i] ^= dbMask[i];
        db[0] &= bitMask;
        int sLen, psLen;
        if (saltOpt == 0) // Auto: recover the salt length from the 0x01 separator.
        {
            psLen = -1;
            for (int i = 0; i < dbLen; i++) { if (db[i] == 0x01) { psLen = i; break; } if (db[i] != 0x00) return false; }
            if (psLen < 0) return false;
            sLen = dbLen - psLen - 1;
        }
        else
        {
            sLen = saltOpt == -1 ? hLen : (int)saltOpt;
            psLen = emLen - hLen - sLen - 2;
            if (psLen < 0) return false;
            for (int i = 0; i < psLen; i++) if (db[i] != 0x00) return false;
            if (db[psLen] != 0x01) return false;
        }
        byte[] salt = new byte[sLen];
        System.Array.Copy(db, dbLen - sLen, salt, 0, sLen);
        byte[] mp = new byte[8 + hLen + sLen];
        System.Array.Copy(mHash, 0, mp, 8, hLen);
        System.Array.Copy(salt, 0, mp, 8 + hLen, sLen);
        byte[] h2 = fn(mp);
        for (int i = 0; i < hLen; i++) if (hh[i] != h2[i]) return false;
        return true;
    }

    private static byte[] Mgf1(byte[] seed, int maskLen, System.Func<byte[], byte[]> fn)
    {
        byte[] mask = new byte[maskLen];
        int outPos = 0;
        for (uint counter = 0; outPos < maskLen; counter++)
        {
            byte[] c = new byte[seed.Length + 4];
            System.Array.Copy(seed, 0, c, 0, seed.Length);
            c[seed.Length] = (byte)(counter >> 24); c[seed.Length + 1] = (byte)(counter >> 16);
            c[seed.Length + 2] = (byte)(counter >> 8); c[seed.Length + 3] = (byte)counter;
            byte[] d = fn(c);
            int take = System.Math.Min(d.Length, maskLen - outPos);
            System.Array.Copy(d, 0, mask, outPos, take);
            outPos += take;
        }
        return mask;
    }

    // Integer-to-octet-string of a fixed length (big-endian, left zero-padded).
    private static byte[] I2OSP(BigInteger v, int len)
    {
        byte[] b = v.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (b.Length == len) return b;
        byte[] p = new byte[len];
        if (b.Length < len) System.Array.Copy(b, 0, p, len - b.Length, b.Length);
        else System.Array.Copy(b, b.Length - len, p, 0, len);
        return p;
    }

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
