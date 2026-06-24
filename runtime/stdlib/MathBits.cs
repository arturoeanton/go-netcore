namespace GoCLR.Stdlib;

using System.Numerics;
using GoCLR.Runtime;

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
    public static uint Reverse16(uint x) { uint r = 0; for (int i = 0; i < 16; i++) { r = (r << 1) | (x & 1); x >>= 1; } return r & 0xffff; }
    public static int Reverse8(int x) { int v = x & 0xff; int r = 0; for (int i = 0; i < 8; i++) { r = (r << 1) | (v & 1); v >>= 1; } return r & 0xff; }

    // Word-size aliases (Go's uint is 64-bit here).
    public static ulong Reverse(ulong x) => Reverse64(x);
    public static ulong ReverseBytes(ulong x) => ReverseBytes64(x);
    public static ulong RotateLeft(ulong x, long k) => RotateLeft64(x, k);

    // Add/Sub: full-word add/subtract with carry/borrow, matching Go's bit tricks exactly.
    public static object?[] Add64(ulong x, ulong y, ulong carry)
    {
        ulong sum = unchecked(x + y + carry);
        ulong carryOut = ((x & y) | ((x | y) & ~sum)) >> 63;
        return new object?[] { sum, carryOut };
    }
    public static object?[] Add(ulong x, ulong y, ulong carry) => Add64(x, y, carry);
    public static object?[] Add32(uint x, uint y, uint carry)
    {
        ulong s = (ulong)x + y + carry;
        return new object?[] { (uint)s, (uint)(s >> 32) };
    }
    public static object?[] Sub64(ulong x, ulong y, ulong borrow)
    {
        ulong diff = unchecked(x - y - borrow);
        ulong borrowOut = ((~x & y) | (~(x ^ y) & diff)) >> 63;
        return new object?[] { diff, borrowOut };
    }
    public static object?[] Sub(ulong x, ulong y, ulong borrow) => Sub64(x, y, borrow);
    public static object?[] Sub32(uint x, uint y, uint borrow)
    {
        ulong d = (ulong)x - y - borrow;
        return new object?[] { (uint)d, (uint)((d >> 32) & 1) };
    }

    // Mul: full-width product into (hi, lo).
    public static object?[] Mul64(ulong x, ulong y) { UInt128 p = (UInt128)x * y; return new object?[] { (ulong)(p >> 64), (ulong)p }; }
    public static object?[] Mul(ulong x, ulong y) => Mul64(x, y);
    public static object?[] Mul32(uint x, uint y) { ulong p = (ulong)x * y; return new object?[] { (uint)(p >> 32), (uint)p }; }

    // Div: (hi,lo)/y -> (quo, rem); panics on divide-by-zero or quotient overflow, like Go.
    public static object?[] Div64(ulong hi, ulong lo, ulong y)
    {
        if (y == 0) throw new GoPanicException(GoString.FromDotNetString("runtime error: integer divide by zero"));
        if (hi >= y) throw new GoPanicException(GoString.FromDotNetString("runtime error: integer overflow"));
        UInt128 d = ((UInt128)hi << 64) | lo;
        return new object?[] { (ulong)(d / y), (ulong)(d % y) };
    }
    public static object?[] Div(ulong hi, ulong lo, ulong y) => Div64(hi, lo, y);
    public static object?[] Div32(uint hi, uint lo, uint y)
    {
        if (y == 0) throw new GoPanicException(GoString.FromDotNetString("runtime error: integer divide by zero"));
        if (hi >= y) throw new GoPanicException(GoString.FromDotNetString("runtime error: integer overflow"));
        ulong d = ((ulong)hi << 32) | lo;
        return new object?[] { (uint)(d / y), (uint)(d % y) };
    }
    // Rem: remainder of (hi,lo)/y (no overflow check).
    public static ulong Rem64(ulong hi, ulong lo, ulong y) { UInt128 d = ((UInt128)hi << 64) | lo; return (ulong)(d % y); }
    public static ulong Rem(ulong hi, ulong lo, ulong y) => Rem64(hi, lo, y);
    public static uint Rem32(uint hi, uint lo, uint y) { ulong d = ((ulong)hi << 32) | lo; return (uint)(d % y); }
}
