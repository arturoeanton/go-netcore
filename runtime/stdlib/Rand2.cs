namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the top-level functions of Go's <c>math/rand/v2</c>, backed by
/// .NET's shared thread-safe random source. The sequence is not seedable/deterministic
/// (matching rand/v2's auto-seeded global), so it is not byte-reproducible across Go.</summary>
public static class Rand2
{
    private static System.Random R => System.Random.Shared;

    public static long IntN(long n) => n <= 0 ? 0 : R.NextInt64(n);
    public static long Int64N(long n) => n <= 0 ? 0 : R.NextInt64(n);
    public static int Int32N(int n) => n <= 0 ? 0 : R.Next(n);
    public static ulong UintN(ulong n) => n == 0 ? 0 : (ulong)R.NextInt64((long)System.Math.Min(n, long.MaxValue));
    public static long Int() => R.NextInt64() & long.MaxValue;
    public static long Int64() => R.NextInt64();
    public static int Int32() => R.Next();
    public static ulong Uint64() => (ulong)R.NextInt64();
    public static uint Uint32() => (uint)R.Next();
    public static double Float64() => R.NextDouble();
    public static double Float32() => R.NextSingle();

    // rand.Shuffle(n, swap): Fisher-Yates over [0,n) via the swap callback.
    public static void Shuffle(long n, GoClosure swap)
    {
        for (long i = n - 1; i > 0; i--)
        {
            long j = R.NextInt64(i + 1);
            GoRuntime.InvokeArgs(swap, i, j);
        }
    }

    // rand.Perm(n): a random permutation of [0,n).
    public static GoSlice Perm(long n)
    {
        var p = new object?[n];
        for (long i = 0; i < n; i++)
        {
            long j = R.NextInt64(i + 1);
            p[i] = p[j];
            p[j] = i;
        }
        return new GoSlice { Data = p, Off = 0, Len = (int)n, Cap = (int)n };
    }
}
