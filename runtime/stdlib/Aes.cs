namespace GoCLR.Stdlib;

using System.Security.Cryptography;
using GoCLR.Runtime;

/// <summary>A cipher.Block (AES) handle.</summary>
public sealed class GoBlock { public byte[] Key = System.Array.Empty<byte>(); }
/// <summary>A cipher.AEAD (AES-GCM) handle.</summary>
public sealed class GoGCM { public byte[] Key = System.Array.Empty<byte>(); }

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
}
