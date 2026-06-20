namespace GoCLR.Stdlib;

using System;
using GoCLR.Runtime;

/// <summary>Shim for Go's <c>bytes</c> package (operations on []byte).
/// []byte is a GoSlice whose elements are boxed bytes (stored as int).</summary>
public static class Bytes
{
    private static byte[] B(GoSlice s)
    {
        var r = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) r[i] = (byte)Convert.ToInt32(s.Data[s.Off + i]);
        return r;
    }
    private static GoSlice S(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static GoSlice Slices(byte[][] parts)
    {
        var d = new object?[parts.Length];
        for (int i = 0; i < parts.Length; i++) d[i] = S(parts[i]);
        return new GoSlice { Data = d, Off = 0, Len = parts.Length, Cap = parts.Length };
    }
    private static long Idx(byte[] s, byte[] sub)
    {
        if (sub.Length == 0) return 0;
        for (int i = 0; i + sub.Length <= s.Length; i++)
        {
            bool m = true;
            for (int j = 0; j < sub.Length; j++) if (s[i + j] != sub[j]) { m = false; break; }
            if (m) return i;
        }
        return -1;
    }

    public static bool Equal(GoSlice a, GoSlice b) => B(a).AsSpan().SequenceEqual(B(b));
    public static long Compare(GoSlice a, GoSlice b) => B(a).AsSpan().SequenceCompareTo(B(b));
    public static bool Contains(GoSlice s, GoSlice sub) => Idx(B(s), B(sub)) >= 0;
    public static bool HasPrefix(GoSlice s, GoSlice p) => B(s).AsSpan().StartsWith(B(p));
    public static bool HasSuffix(GoSlice s, GoSlice p) => B(s).AsSpan().EndsWith(B(p));
    public static long Index(GoSlice s, GoSlice sub) => Idx(B(s), B(sub));
    public static long LastIndex(GoSlice s, GoSlice sub)
    {
        byte[] b = B(s), p = B(sub);
        if (p.Length == 0) return b.Length;
        for (int i = b.Length - p.Length; i >= 0; i--)
        {
            bool m = true;
            for (int j = 0; j < p.Length; j++) if (b[i + j] != p[j]) { m = false; break; }
            if (m) return i;
        }
        return -1;
    }
    public static long LastIndexByte(GoSlice s, int c)
    {
        byte[] b = B(s);
        for (int i = b.Length - 1; i >= 0; i--) if (b[i] == (byte)c) return i;
        return -1;
    }
    public static long IndexByte(GoSlice s, int c)
    {
        var b = B(s);
        for (int i = 0; i < b.Length; i++) if (b[i] == (byte)c) return i;
        return -1;
    }
    public static long Count(GoSlice s, GoSlice sub)
    {
        byte[] b = B(s), sb = B(sub);
        if (sb.Length == 0) return b.Length + 1;
        long n = 0; long i = 0;
        while (true)
        {
            long k = Idx(b.AsSpan((int)i).ToArray(), sb);
            if (k < 0) break;
            n++; i += k + sb.Length;
        }
        return n;
    }

    public static GoSlice ToUpper(GoSlice s)
    {
        var b = B(s);
        for (int i = 0; i < b.Length; i++) if (b[i] >= (byte)'a' && b[i] <= (byte)'z') b[i] -= 32;
        return S(b);
    }
    public static GoSlice ToLower(GoSlice s)
    {
        var b = B(s);
        for (int i = 0; i < b.Length; i++) if (b[i] >= (byte)'A' && b[i] <= (byte)'Z') b[i] += 32;
        return S(b);
    }
    public static GoSlice TrimSpace(GoSlice s)
    {
        var str = System.Text.Encoding.UTF8.GetString(B(s)).Trim();
        return S(System.Text.Encoding.UTF8.GetBytes(str));
    }
    public static GoSlice Repeat(GoSlice s, long count)
    {
        var b = B(s);
        var outb = new byte[b.Length * (count < 0 ? 0 : (int)count)];
        for (int i = 0; i < (int)count; i++) Array.Copy(b, 0, outb, i * b.Length, b.Length);
        return S(outb);
    }

    public static GoSlice Split(GoSlice s, GoSlice sep)
    {
        byte[] b = B(s), sb = B(sep);
        var parts = new System.Collections.Generic.List<byte[]>();
        if (sb.Length == 0)
        {
            foreach (var by in b) parts.Add(new[] { by });
            return Slices(parts.ToArray());
        }
        int start = 0;
        while (true)
        {
            long k = Idx(b.AsSpan(start).ToArray(), sb);
            if (k < 0) { parts.Add(b.AsSpan(start).ToArray()); break; }
            parts.Add(b.AsSpan(start, (int)k).ToArray());
            start += (int)k + sb.Length;
        }
        return Slices(parts.ToArray());
    }

    public static GoSlice SplitAfterN(GoSlice s, GoSlice sep, long n)
    {
        if (n == 0) return Slices(System.Array.Empty<byte[]>());
        byte[] b = B(s), sb = B(sep);
        var parts = new System.Collections.Generic.List<byte[]>();
        int start = 0;
        while (sb.Length > 0 && (n < 0 || parts.Count < n - 1))
        {
            long k = Idx(b.AsSpan(start).ToArray(), sb);
            if (k < 0) break;
            int end = start + (int)k + sb.Length;
            parts.Add(b.AsSpan(start, end - start).ToArray());
            start = end;
        }
        parts.Add(b.AsSpan(start).ToArray());
        return Slices(parts.ToArray());
    }

    public static GoSlice Join(GoSlice elems, GoSlice sep)
    {
        byte[] sb = B(sep);
        var outb = new System.Collections.Generic.List<byte>();
        for (int i = 0; i < elems.Len; i++)
        {
            if (i > 0) outb.AddRange(sb);
            outb.AddRange(B((GoSlice)elems.Data[elems.Off + i]!));
        }
        return S(outb.ToArray());
    }
}
