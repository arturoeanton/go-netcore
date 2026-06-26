namespace GoCLR.Stdlib;

using System.Security.Cryptography;
using GoCLR.Runtime;

/// <summary>A cipher.Block (AES) handle.</summary>
public sealed class GoBlock { public byte[] Key = System.Array.Empty<byte>(); }
/// <summary>A cipher.AEAD (AES-GCM) handle.</summary>
public sealed class GoGCM { public byte[] Key = System.Array.Empty<byte>(); }
/// <summary>A cipher.BlockMode (AES-CBC): the chaining IV advances across CryptBlocks calls.</summary>
public sealed class GoCBC { public byte[] Key = System.Array.Empty<byte>(); public byte[] IV = System.Array.Empty<byte>(); public bool Encrypt; }
/// <summary>A cipher.Stream (AES-CTR): a big-endian counter + the current keystream block.</summary>
public sealed class GoCTR { public byte[] Key = System.Array.Empty<byte>(); public byte[] Counter = System.Array.Empty<byte>(); public byte[] KeyStream = new byte[16]; public int Used = 16; }

/// <summary>Shim for crypto/aes + crypto/cipher AES-GCM (the common AEAD path).</summary>
public static class Aes
{
    private static byte[] B(GoSlice s) { var b = new byte[s.Len]; for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]); return b; }
    private static GoSlice S(byte[] b) { var d = new object?[b.Length]; for (int i = 0; i < b.Length; i++) d[i] = (int)b[i]; return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length }; }

    public static object?[] NewCipher(GoSlice key)
    {
        int n = key.Len;
        if (n != 16 && n != 24 && n != 32) return new object?[] { null, new GoError(GoString.FromDotNetString("crypto/aes: invalid key size " + n)) };
        return new object?[] { new GoBlock { Key = B(key) }, null };
    }

    public static object?[] NewGCM(object block) => new object?[] { new GoGCM { Key = ((GoBlock)block).Key }, null };

    public static long GCM_NonceSize(object g) => 12;
    public static long GCM_Overhead(object g) => 16;

    public static GoSlice GCM_Seal(object g, GoSlice dst, GoSlice nonce, GoSlice plaintext, GoSlice additional)
    {
        var gcm = new AesGcm(((GoGCM)g).Key, 16);
        byte[] pt = B(plaintext), n = B(nonce);
        byte[] ct = new byte[pt.Length], tag = new byte[16];
        gcm.Encrypt(n, pt, ct, tag, additional.Data == null ? null : B(additional));
        var outp = new byte[ct.Length + 16];
        ct.CopyTo(outp, 0); tag.CopyTo(outp, ct.Length);
        return S(outp);
    }
    public static object?[] GCM_Open(object g, GoSlice dst, GoSlice nonce, GoSlice ciphertext, GoSlice additional)
    {
        try
        {
            var gcm = new AesGcm(((GoGCM)g).Key, 16);
            byte[] all = B(ciphertext), n = B(nonce);
            if (all.Length < 16) return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("cipher: message authentication failed")) };
            byte[] ct = new byte[all.Length - 16], tag = new byte[16];
            System.Array.Copy(all, 0, ct, 0, ct.Length); System.Array.Copy(all, ct.Length, tag, 0, 16);
            byte[] pt = new byte[ct.Length];
            gcm.Decrypt(n, ct, tag, pt, additional.Data == null ? null : B(additional));
            return new object?[] { S(pt), null };
        }
        catch { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("cipher: message authentication failed")) }; }
    }

    private static void Into(GoSlice dst, byte[] bytes)
    {
        for (int i = 0; i < bytes.Length && i < dst.Len; i++) dst.Data![dst.Off + i] = (int)bytes[i];
    }

    // --- cipher.NewCBCEncrypter / NewCBCDecrypter (AES-CBC, no padding like Go) ---
    public static object NewCBCEncrypter(object block, GoSlice iv) => new GoCBC { Key = ((GoBlock)block).Key, IV = B(iv), Encrypt = true };
    public static object NewCBCDecrypter(object block, GoSlice iv) => new GoCBC { Key = ((GoBlock)block).Key, IV = B(iv), Encrypt = false };
    public static long CBC_BlockSize(object c) => 16;
    public static void CBC_CryptBlocks(object c, GoSlice dst, GoSlice src)
    {
        var cbc = (GoCBC)c;
        byte[] input = B(src);
        if (input.Length == 0) return;
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = cbc.Key; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None; aes.IV = cbc.IV;
        byte[] outp = cbc.Encrypt
            ? aes.CreateEncryptor().TransformFinalBlock(input, 0, input.Length)
            : aes.CreateDecryptor().TransformFinalBlock(input, 0, input.Length);
        // The IV for the next call is the last ciphertext block (output for encrypt, input for decrypt).
        byte[] chain = cbc.Encrypt ? outp : input;
        System.Array.Copy(chain, chain.Length - 16, cbc.IV, 0, 16);
        Into(dst, outp);
    }

    // --- cipher.NewCTR (AES-CTR: XOR with the AES-ECB encryption of a big-endian counter) ---
    public static object NewCTR(object block, GoSlice iv) => new GoCTR { Key = ((GoBlock)block).Key, Counter = B(iv) };
    public static void CTR_XORKeyStream(object c, GoSlice dst, GoSlice src)
    {
        var ctr = (GoCTR)c;
        byte[] input = B(src);
        byte[] outp = new byte[input.Length];
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = ctr.Key; aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.None;
        var enc = aes.CreateEncryptor();
        for (int i = 0; i < input.Length; i++)
        {
            if (ctr.Used == 16)
            {
                ctr.KeyStream = enc.TransformFinalBlock(ctr.Counter, 0, 16);
                for (int j = 15; j >= 0; j--) { if (++ctr.Counter[j] != 0) break; } // big-endian increment
                ctr.Used = 0;
            }
            outp[i] = (byte)(input[i] ^ ctr.KeyStream[ctr.Used++]);
        }
        Into(dst, outp);
    }
}
