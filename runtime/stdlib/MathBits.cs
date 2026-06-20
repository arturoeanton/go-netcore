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
}
