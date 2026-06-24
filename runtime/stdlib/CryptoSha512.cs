namespace GoCLR.Stdlib;

/// <summary>SHA-512 compression core (FIPS 180-4), used for the SHA-512/224 and SHA-512/256
/// truncated variants which have no .NET primitive — they reuse the SHA-512 round function
/// with different initial hash values. Exact uint64 arithmetic, so byte-exact with Go.</summary>
public static partial class Crypto
{
    internal static readonly ulong[] Iv512_224 =
    {
        0x8C3D37C819544DA2, 0x73E1996689DCD4D6, 0x1DFAB7AE32FF9C82, 0x679DD514582F9FCF,
        0x0F6D2B697BD44DA8, 0x77E36F7304C48942, 0x3F9D85A86A1D36C8, 0x1112E6AD91D692A1,
    };
    internal static readonly ulong[] Iv512_256 =
    {
        0x22312194FC2BF72C, 0x9F555FA3C84C64C2, 0x2393B86B6F53B151, 0x963877195940EABD,
        0x96283EE2A88EFFE3, 0xBE5E1E2553863992, 0x2B0199FC2C85B8AA, 0x0EB72DDC81C52CA2,
    };

    private static readonly ulong[] K512 =
    {
        0x428A2F98D728AE22, 0x7137449123EF65CD, 0xB5C0FBCFEC4D3B2F, 0xE9B5DBA58189DBBC,
        0x3956C25BF348B538, 0x59F111F1B605D019, 0x923F82A4AF194F9B, 0xAB1C5ED5DA6D8118,
        0xD807AA98A3030242, 0x12835B0145706FBE, 0x243185BE4EE4B28C, 0x550C7DC3D5FFB4E2,
        0x72BE5D74F27B896F, 0x80DEB1FE3B1696B1, 0x9BDC06A725C71235, 0xC19BF174CF692694,
        0xE49B69C19EF14AD2, 0xEFBE4786384F25E3, 0x0FC19DC68B8CD5B5, 0x240CA1CC77AC9C65,
        0x2DE92C6F592B0275, 0x4A7484AA6EA6E483, 0x5CB0A9DCBD41FBD4, 0x76F988DA831153B5,
        0x983E5152EE66DFAB, 0xA831C66D2DB43210, 0xB00327C898FB213F, 0xBF597FC7BEEF0EE4,
        0xC6E00BF33DA88FC2, 0xD5A79147930AA725, 0x06CA6351E003826F, 0x142929670A0E6E70,
        0x27B70A8546D22FFC, 0x2E1B21385C26C926, 0x4D2C6DFC5AC42AED, 0x53380D139D95B3DF,
        0x650A73548BAF63DE, 0x766A0ABB3C77B2A8, 0x81C2C92E47EDAEE6, 0x92722C851482353B,
        0xA2BFE8A14CF10364, 0xA81A664BBC423001, 0xC24B8B70D0F89791, 0xC76C51A30654BE30,
        0xD192E819D6EF5218, 0xD69906245565A910, 0xF40E35855771202A, 0x106AA07032BBD1B8,
        0x19A4C116B8D2D0C8, 0x1E376C085141AB53, 0x2748774CDF8EEB99, 0x34B0BCB5E19B48A8,
        0x391C0CB3C5C95A63, 0x4ED8AA4AE3418ACB, 0x5B9CCA4F7763E373, 0x682E6FF3D6B2B8A3,
        0x748F82EE5DEFB2FC, 0x78A5636F43172F60, 0x84C87814A1F0AB72, 0x8CC702081A6439EC,
        0x90BEFFFA23631E28, 0xA4506CEBDE82BDE9, 0xBEF9A3F7B2C67915, 0xC67178F2E372532B,
        0xCA273ECEEA26619C, 0xD186B8C721C0C207, 0xEADA7DD6CDE0EB1E, 0xF57D4F7FEE6ED178,
        0x06F067AA72176FBA, 0x0A637DC5A2C898A6, 0x113F9804BEF90DAE, 0x1B710B35131C471B,
        0x28DB77F523047D84, 0x32CAAB7B40C72493, 0x3C9EBE0A15C9BEBC, 0x431D67C49C100D4C,
        0x4CC5D4BECB3E42B6, 0x597F299CFC657E2A, 0x5FCB6FAB3AD6FAEC, 0x6C44198C4A475817,
    };

    // SHA-256 core with the FIPS 180-4 SHA-224 alternate IV (.NET has no SHA-224 primitive,
    // and SHA-224 is NOT a truncation of SHA-256 — it uses a different IV).
    internal static readonly uint[] Iv224 =
    {
        0xC1059ED8, 0x367CD507, 0x3070DD17, 0xF70E5939, 0xFFC00B31, 0x68581511, 0x64F98FA7, 0xBEFA4FA4,
    };
    private static readonly uint[] K256 =
    {
        0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
        0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
        0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
        0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
        0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13, 0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
        0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
        0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
        0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2,
    };
    private static uint Rotr32(uint x, int n) => (x >> n) | (x << (32 - n));
    internal static byte[] Sha256Core(byte[] msg, uint[] iv, int outLen)
    {
        uint[] h = (uint[])iv.Clone();
        ulong ml = (ulong)msg.Length * 8;
        int rem = (msg.Length + 1) % 64;
        int padLen = (56 - rem + 64) % 64;
        var data = new byte[msg.Length + 1 + padLen + 8];
        System.Array.Copy(msg, data, msg.Length);
        data[msg.Length] = 0x80;
        for (int i = 0; i < 8; i++) data[data.Length - 1 - i] = (byte)(ml >> (8 * i));
        var w = new uint[64];
        for (int off = 0; off < data.Length; off += 64)
        {
            for (int i = 0; i < 16; i++)
            {
                uint x = 0;
                for (int j = 0; j < 4; j++) x = (x << 8) | data[off + i * 4 + j];
                w[i] = x;
            }
            for (int i = 16; i < 64; i++)
            {
                uint s0 = Rotr32(w[i - 15], 7) ^ Rotr32(w[i - 15], 18) ^ (w[i - 15] >> 3);
                uint s1 = Rotr32(w[i - 2], 17) ^ Rotr32(w[i - 2], 19) ^ (w[i - 2] >> 10);
                w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
            }
            uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], hh = h[7];
            for (int i = 0; i < 64; i++)
            {
                uint S1 = Rotr32(e, 6) ^ Rotr32(e, 11) ^ Rotr32(e, 25);
                uint ch = (e & f) ^ (~e & g);
                uint t1 = unchecked(hh + S1 + ch + K256[i] + w[i]);
                uint S0 = Rotr32(a, 2) ^ Rotr32(a, 13) ^ Rotr32(a, 22);
                uint maj = (a & b) ^ (a & c) ^ (b & c);
                uint t2 = unchecked(S0 + maj);
                hh = g; g = f; f = e; e = unchecked(d + t1); d = c; c = b; b = a; a = unchecked(t1 + t2);
            }
            h[0] = unchecked(h[0] + a); h[1] = unchecked(h[1] + b); h[2] = unchecked(h[2] + c); h[3] = unchecked(h[3] + d);
            h[4] = unchecked(h[4] + e); h[5] = unchecked(h[5] + f); h[6] = unchecked(h[6] + g); h[7] = unchecked(h[7] + hh);
        }
        var full = new byte[32];
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 4; j++) full[i * 4 + j] = (byte)(h[i] >> (24 - 8 * j));
        return full[..outLen];
    }

    private static ulong Rotr(ulong x, int n) => (x >> n) | (x << (64 - n));

    private static byte[] Sha512Core(byte[] msg, ulong[] iv, int outLen)
    {
        ulong[] h = (ulong[])iv.Clone();
        ulong ml = (ulong)msg.Length * 8;
        int rem = (msg.Length + 1) % 128;
        int padLen = (112 - rem + 128) % 128;
        var data = new byte[msg.Length + 1 + padLen + 16];
        System.Array.Copy(msg, data, msg.Length);
        data[msg.Length] = 0x80;
        for (int i = 0; i < 8; i++) data[data.Length - 1 - i] = (byte)(ml >> (8 * i)); // low 64 bits, big-endian

        var w = new ulong[80];
        for (int off = 0; off < data.Length; off += 128)
        {
            for (int i = 0; i < 16; i++)
            {
                ulong x = 0;
                for (int j = 0; j < 8; j++) x = (x << 8) | data[off + i * 8 + j];
                w[i] = x;
            }
            for (int i = 16; i < 80; i++)
            {
                ulong s0 = Rotr(w[i - 15], 1) ^ Rotr(w[i - 15], 8) ^ (w[i - 15] >> 7);
                ulong s1 = Rotr(w[i - 2], 19) ^ Rotr(w[i - 2], 61) ^ (w[i - 2] >> 6);
                w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
            }
            ulong a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], hh = h[7];
            for (int i = 0; i < 80; i++)
            {
                ulong S1 = Rotr(e, 14) ^ Rotr(e, 18) ^ Rotr(e, 41);
                ulong ch = (e & f) ^ (~e & g);
                ulong t1 = unchecked(hh + S1 + ch + K512[i] + w[i]);
                ulong S0 = Rotr(a, 28) ^ Rotr(a, 34) ^ Rotr(a, 39);
                ulong maj = (a & b) ^ (a & c) ^ (b & c);
                ulong t2 = unchecked(S0 + maj);
                hh = g; g = f; f = e; e = unchecked(d + t1); d = c; c = b; b = a; a = unchecked(t1 + t2);
            }
            h[0] = unchecked(h[0] + a); h[1] = unchecked(h[1] + b); h[2] = unchecked(h[2] + c); h[3] = unchecked(h[3] + d);
            h[4] = unchecked(h[4] + e); h[5] = unchecked(h[5] + f); h[6] = unchecked(h[6] + g); h[7] = unchecked(h[7] + hh);
        }
        var full = new byte[64];
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++) full[i * 8 + j] = (byte)(h[i] >> (56 - 8 * j));
        return full[..outLen];
    }
}
