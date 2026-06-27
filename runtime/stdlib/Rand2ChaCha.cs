namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>math/rand/v2.ChaCha8: the ChaCha8-based random source (Go's
/// internal/chacha8rand), deterministic given its 32-byte seed. Ported byte-exact from
/// the generic block function and the State refill/reseed machine.</summary>
[GoShim("math/rand/v2.ChaCha8")]
public sealed class GoChaCha8
{
    public ulong[] Buf = new ulong[32];
    public ulong[] Seed = new ulong[4];
    public uint I, N, C;
}

public static partial class Rand2
{
    // chacha8rand constants.
    private const uint ChCtrInc = 4, ChCtrMax = 16, ChChunk = 32, ChReseed = 4;

    public static object NewChaCha8(GoSlice seed)
    {
        byte[] s = Raw(seed);
        var c = new GoChaCha8();
        var s4 = new ulong[4];
        for (int k = 0; k < 4; k++) s4[k] = ChLE64(s, k * 8);
        ChInit64(c, s4);
        return c;
    }

    // (*ChaCha8).Seed([32]byte): re-seed in place.
    public static void ChaCha8_Seed(object co, GoSlice seed)
    {
        byte[] s = Raw(seed);
        var s4 = new ulong[4];
        for (int k = 0; k < 4; k++) s4[k] = ChLE64(s, k * 8);
        ChInit64((GoChaCha8)co, s4);
    }

    public static ulong ChaCha8_Uint64(object co)
    {
        var c = (GoChaCha8)co;
        while (true)
        {
            if (c.I < c.N) { ulong x = c.Buf[c.I & 31]; c.I++; return x; }
            ChRefill(c);
        }
    }

    private static ulong ChLE64(byte[] b, int o)
    {
        ulong v = 0;
        for (int i = 0; i < 8; i++) v |= (ulong)b[o + i] << (8 * i);
        return v;
    }

    private static void ChInit64(GoChaCha8 c, ulong[] seed)
    {
        seed.CopyTo(c.Seed, 0);
        ChBlock(c.Seed, c.Buf, 0);
        c.C = 0; c.I = 0; c.N = ChChunk;
    }

    private static void ChRefill(GoChaCha8 c)
    {
        c.C += ChCtrInc;
        if (c.C == ChCtrMax)
        {
            for (int k = 0; k < (int)ChReseed; k++) c.Seed[k] = c.Buf[c.Buf.Length - (int)ChReseed + k];
            c.C = 0;
        }
        ChBlock(c.Seed, c.Buf, c.C);
        c.I = 0;
        c.N = (uint)c.Buf.Length;
        if (c.C == ChCtrMax - ChCtrInc) c.N = (uint)c.Buf.Length - ChReseed;
    }

    private static void ChQr(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b; d ^= a; d = (d << 16) | (d >> 16);
        c += d; b ^= c; b = (b << 12) | (b >> 20);
        a += b; d ^= a; d = (d << 8) | (d >> 24);
        c += d; b ^= c; b = (b << 7) | (b >> 25);
    }

    private static void ChSetRow(uint[] b, int row, uint v) { int o = row * 4; b[o] = v; b[o + 1] = v; b[o + 2] = v; b[o + 3] = v; }

    // The generic ChaCha8 block: buf[32]uint64 is the [16][4]uint32 state in row-major order.
    private static void ChBlock(ulong[] seed, ulong[] buf, uint counter)
    {
        var b = new uint[64]; // b[row*4 + lane]
        ChSetRow(b, 0, 0x61707865u);
        ChSetRow(b, 1, 0x3320646eu);
        ChSetRow(b, 2, 0x79622d32u);
        ChSetRow(b, 3, 0x6b206574u);
        ChSetRow(b, 4, (uint)seed[0]);
        ChSetRow(b, 5, (uint)(seed[0] >> 32));
        ChSetRow(b, 6, (uint)seed[1]);
        ChSetRow(b, 7, (uint)(seed[1] >> 32));
        ChSetRow(b, 8, (uint)seed[2]);
        ChSetRow(b, 9, (uint)(seed[2] >> 32));
        ChSetRow(b, 10, (uint)seed[3]);
        ChSetRow(b, 11, (uint)(seed[3] >> 32));
        // Counter row: the four lanes are counter, counter+1, counter+2, counter+3 (LE).
        b[48] = counter; b[49] = counter + 1; b[50] = counter + 2; b[51] = counter + 3;
        // rows 13..15 stay zero.
        for (int i = 0; i < 4; i++)
        {
            uint b0 = b[i], b1 = b[4 + i], b2 = b[8 + i], b3 = b[12 + i], b4 = b[16 + i], b5 = b[20 + i],
                 b6 = b[24 + i], b7 = b[28 + i], b8 = b[32 + i], b9 = b[36 + i], b10 = b[40 + i], b11 = b[44 + i],
                 b12 = b[48 + i], b13 = b[52 + i], b14 = b[56 + i], b15 = b[60 + i];
            for (int round = 0; round < 4; round++)
            {
                ChQr(ref b0, ref b4, ref b8, ref b12);
                ChQr(ref b1, ref b5, ref b9, ref b13);
                ChQr(ref b2, ref b6, ref b10, ref b14);
                ChQr(ref b3, ref b7, ref b11, ref b15);
                ChQr(ref b0, ref b5, ref b10, ref b15);
                ChQr(ref b1, ref b6, ref b11, ref b12);
                ChQr(ref b2, ref b7, ref b8, ref b13);
                ChQr(ref b3, ref b4, ref b9, ref b14);
            }
            b[i] = b0; b[4 + i] = b1; b[8 + i] = b2; b[12 + i] = b3;
            b[16 + i] += b4; b[20 + i] += b5; b[24 + i] += b6; b[28 + i] += b7;
            b[32 + i] += b8; b[36 + i] += b9; b[40 + i] += b10; b[44 + i] += b11;
            b[48 + i] = b12; b[52 + i] = b13; b[56 + i] = b14; b[60 + i] = b15;
        }
        for (int k = 0; k < 32; k++) buf[k] = (ulong)b[2 * k] | ((ulong)b[2 * k + 1] << 32);
    }
}
