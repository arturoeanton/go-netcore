namespace GoCLR.Stdlib;

using System.Numerics;

/// <summary>Shim for Go's <c>math/bits</c>. uint/uint64 are ulong; int results are long.</summary>
public static class MathBits
{
    public static long OnesCount(ulong x) => BitOperations.PopCount(x);
    public static long OnesCount64(ulong x) => BitOperations.PopCount(x);
    public static long OnesCount32(uint x) => BitOperations.PopCount(x);
    public static long LeadingZeros64(ulong x) => BitOperations.LeadingZeroCount(x);
    public static long LeadingZeros(ulong x) => x == 0 ? 64 : BitOperations.LeadingZeroCount(x);
    public static long TrailingZeros64(ulong x) => x == 0 ? 64 : BitOperations.TrailingZeroCount(x);
    public static long TrailingZeros(ulong x) => x == 0 ? 64 : BitOperations.TrailingZeroCount(x);
    public static long Len64(ulong x) => 64 - BitOperations.LeadingZeroCount(x);
    public static long Len(ulong x) => 64 - BitOperations.LeadingZeroCount(x);
    public static ulong RotateLeft64(ulong x, long k) => BitOperations.RotateLeft(x, (int)k);
    public static ulong Reverse64(ulong x)
    {
        ulong r = 0;
        for (int i = 0; i < 64; i++) { r = (r << 1) | (x & 1); x >>= 1; }
        return r;
    }
    public static ulong ReverseBytes64(ulong x) => BinaryPrimitivesReverse(x);
    private static ulong BinaryPrimitivesReverse(ulong x) => System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(x);

    // 8/16-bit variants: uint8/uint16 box to Int32; mask to the width.
    public static long OnesCount8(int x) => BitOperations.PopCount((uint)(x & 0xff));
    public static long OnesCount16(uint x) => BitOperations.PopCount(x & 0xffff);
    public static long LeadingZeros8(int x) => 8 - Len8(x);
    public static long LeadingZeros16(uint x) => 16 - Len16(x);
    public static long LeadingZeros32(uint x) => 32 - Len32(x);
    public static long TrailingZeros8(int x) { x &= 0xff; return x == 0 ? 8 : BitOperations.TrailingZeroCount((uint)x); }
    public static long TrailingZeros16(uint x) { x &= 0xffff; return x == 0 ? 16 : BitOperations.TrailingZeroCount(x); }
    public static long TrailingZeros32(uint x) => x == 0 ? 32 : BitOperations.TrailingZeroCount(x);
    public static long Len8(int x) { x &= 0xff; long n = 0; while (x > 0) { n++; x >>= 1; } return n; }
    public static long Len16(uint x) { x &= 0xffff; long n = 0; while (x > 0) { n++; x >>= 1; } return n; }
    public static long Len32(uint x) => x == 0 ? 0 : 32 - BitOperations.LeadingZeroCount(x);
    public static int RotateLeft8(int x, long k) { int b = x & 0xff; int s = (int)(((k % 8) + 8) % 8); return (byte)((b << s) | (b >> (8 - s))); }
    public static uint RotateLeft16(uint x, long k) { uint b = x & 0xffff; int s = (int)(((k % 16) + 16) % 16); return (ushort)((b << s) | (b >> (16 - s))); }
    public static uint RotateLeft32(uint x, long k) => BitOperations.RotateLeft(x, (int)k);
    public static uint ReverseBytes16(uint x) => System.Buffers.Binary.BinaryPrimitives.ReverseEndianness((ushort)x);
    public static uint ReverseBytes32(uint x) => System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(x);
    public static uint Reverse32(uint x) { uint r = 0; for (int i = 0; i < 32; i++) { r = (r << 1) | (x & 1); x >>= 1; } return r; }
}
