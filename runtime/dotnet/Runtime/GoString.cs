using System.Text;

namespace GoCLR.Runtime;

/// <summary>
/// GoString models a Go string: an immutable sequence of bytes (UTF-8 by
/// convention, but not required to be valid UTF-8). Indexing yields a byte, and
/// <c>len</c> is the byte length — matching Go semantics rather than .NET's
/// UTF-16 <see cref="string"/>. A decoded .NET string is cached lazily for
/// interop with goja/JSON/regexp.
/// </summary>
public readonly struct GoString : IEquatable<GoString>
{
    private readonly byte[] _utf8;

    public static readonly GoString Empty = new(Array.Empty<byte>());

    private GoString(byte[] utf8)
    {
        _utf8 = utf8;
    }

    /// <summary>Byte length (Go <c>len(s)</c>).</summary>
    public int Len => _utf8?.Length ?? 0;

    /// <summary>Returns the byte at index i (Go <c>s[i]</c>).</summary>
    public byte ByteAt(int index)
    {
        if (_utf8 == null || (uint)index >= (uint)_utf8.Length)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: index out of range"));
        return _utf8[index];
    }

    /// <summary>Raw UTF-8 bytes. The caller must not mutate the returned array.</summary>
    public byte[] Bytes => _utf8 ?? Array.Empty<byte>();

    /// <summary>Builds a GoString from raw bytes (copies the slice).</summary>
    public static GoString FromBytes(ReadOnlySpan<byte> bytes) => new(bytes.ToArray());

    /// <summary>Builds a GoString from raw bytes without copying. Caller transfers ownership.</summary>
    public static GoString FromBytesOwned(byte[] bytes) => new(bytes);

    /// <summary>Encodes a .NET UTF-16 string to a GoString (UTF-8).</summary>
    public static GoString FromDotNetString(string s) => new(Encoding.UTF8.GetBytes(s ?? string.Empty));

    /// <summary>Decodes the bytes as UTF-8 to a .NET string.</summary>
    public string ToDotNetString() => _utf8 == null ? string.Empty : Encoding.UTF8.GetString(_utf8);

    /// <summary>Returns the bytes (alias of <see cref="Bytes"/>) for Go <c>[]byte(s)</c>.</summary>
    public byte[] ToBytes() => (byte[])Bytes.Clone();

    /// <summary>Decodes to runes (Go <c>[]rune(s)</c>). Invalid bytes become U+FFFD.</summary>
    public int[] ToRunes()
    {
        var runes = new List<int>(Len);
        var span = Bytes.AsSpan();
        int i = 0;
        while (i < span.Length)
        {
            var (r, size) = DecodeRune(span[i..]);
            runes.Add(r);
            i += size;
        }
        return runes.ToArray();
    }

    /// <summary>
    /// Decodes the first rune in the byte span, returning the rune and the number
    /// of bytes consumed. Matches unicode/utf8.DecodeRune behaviour (U+FFFD,1 on
    /// error).
    /// </summary>
    public static (int rune, int size) DecodeRune(ReadOnlySpan<byte> b)
    {
        if (b.Length == 0) return (0xFFFD, 0);
        byte b0 = b[0];
        if (b0 < 0x80) return (b0, 1);
        if ((b0 & 0xE0) == 0xC0 && b.Length >= 2 && IsCont(b[1]))
            return (((b0 & 0x1F) << 6) | (b[1] & 0x3F), 2);
        if ((b0 & 0xF0) == 0xE0 && b.Length >= 3 && IsCont(b[1]) && IsCont(b[2]))
            return (((b0 & 0x0F) << 12) | ((b[1] & 0x3F) << 6) | (b[2] & 0x3F), 3);
        if ((b0 & 0xF8) == 0xF0 && b.Length >= 4 && IsCont(b[1]) && IsCont(b[2]) && IsCont(b[3]))
            return (((b0 & 0x07) << 18) | ((b[1] & 0x3F) << 12) | ((b[2] & 0x3F) << 6) | (b[3] & 0x3F), 4);
        return (0xFFFD, 1);
    }

    private static bool IsCont(byte b) => (b & 0xC0) == 0x80;

    /// <summary>Concatenation (Go <c>a + b</c>).</summary>
    public static GoString Concat(GoString a, GoString b)
    {
        var buf = new byte[a.Len + b.Len];
        a.Bytes.CopyTo(buf, 0);
        b.Bytes.CopyTo(buf, a.Len);
        return new GoString(buf);
    }

    public bool Equals(GoString other) => Bytes.AsSpan().SequenceEqual(other.Bytes);
    public override bool Equals(object? obj) => obj is GoString g && Equals(g);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.AddBytes(Bytes);
        return h.ToHashCode();
    }

    /// <summary>Go-style ordering (lexicographic over bytes).</summary>
    public static int Compare(GoString a, GoString b) => a.Bytes.AsSpan().SequenceCompareTo(b.Bytes);

    public static bool operator ==(GoString a, GoString b) => a.Equals(b);
    public static bool operator !=(GoString a, GoString b) => !a.Equals(b);

    public override string ToString() => ToDotNetString();
}
