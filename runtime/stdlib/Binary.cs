namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A binary.ByteOrder handle (little- or big-endian).</summary>
public sealed class GoByteOrder { public bool Big; }

/// <summary>Shim for Go's <c>encoding/binary</c> LittleEndian/BigEndian fixed-width
/// Uint/Put helpers over a []byte (GoSlice).</summary>
public static class Binary
{
    // Varint (LEB128) codecs.
    public static long PutUvarint(GoSlice buf, ulong x)
    {
        int i = 0;
        while (x >= 0x80) { buf.Data![buf.Off + i] = (int)(byte)(x | 0x80); x >>= 7; i++; }
        buf.Data![buf.Off + i] = (int)(byte)x;
        return i + 1;
    }
    public static object?[] Uvarint(GoSlice buf)
    {
        ulong x = 0; int s = 0;
        for (int i = 0; i < buf.Len; i++)
        {
            ulong b = (ulong)(System.Convert.ToInt64(buf.Data![buf.Off + i]) & 0xff);
            if (b < 0x80)
            {
                if (i > 9 || (i == 9 && b > 1)) return new object?[] { (ulong)0, (long)(-(i + 1)) };
                return new object?[] { x | (b << s), (long)(i + 1) };
            }
            x |= (b & 0x7f) << s; s += 7;
        }
        return new object?[] { (ulong)0, 0L };
    }
    public static long PutVarint(GoSlice buf, long v)
    {
        ulong ux = (ulong)v << 1;
        if (v < 0) ux = ~ux;
        return PutUvarint(buf, ux);
    }
    public static object?[] Varint(GoSlice buf)
    {
        var r = Uvarint(buf);
        ulong ux = (ulong)r[0]!;
        long x = (long)(ux >> 1);
        if ((ux & 1) != 0) x = ~x;
        return new object?[] { x, r[1] };
    }

    public static object LittleEndian() => new GoByteOrder { Big = false };
    public static object BigEndian() => new GoByteOrder { Big = true };
    public static object NativeEndian() => new GoByteOrder { Big = false }; // x86/ARM are little-endian

    private static GoSlice AppendBytes(GoSlice buf, System.Collections.Generic.List<byte> add)
    {
        var d = new object?[add.Count];
        for (int i = 0; i < add.Count; i++) d[i] = (int)add[i];
        return Rt.AppendSlice(buf, new GoSlice { Data = d, Off = 0, Len = add.Count, Cap = add.Count });
    }
    public static GoSlice AppendUvarint(GoSlice buf, ulong x)
    {
        var add = new System.Collections.Generic.List<byte>();
        while (x >= 0x80) { add.Add((byte)(x | 0x80)); x >>= 7; }
        add.Add((byte)x);
        return AppendBytes(buf, add);
    }
    public static GoSlice AppendVarint(GoSlice buf, long v)
    {
        ulong ux = (ulong)v << 1;
        if (v < 0) ux = ~ux;
        return AppendUvarint(buf, ux);
    }
    public static object?[] Append(GoSlice buf, object order, object? data)
    {
        var add = new System.Collections.Generic.List<byte>();
        Ser(add, ((GoByteOrder)order).Big, data);
        return new object?[] { AppendBytes(buf, add), null };
    }
    public static object?[] Encode(GoSlice buf, object order, object? data)
    {
        var ser = new System.Collections.Generic.List<byte>();
        Ser(ser, ((GoByteOrder)order).Big, data);
        if (ser.Count > buf.Len) return new object?[] { 0L, new GoError(GoString.FromDotNetString("binary.Encode: buffer too small")) };
        for (int i = 0; i < ser.Count; i++) buf.Data![buf.Off + i] = (int)ser[i];
        return new object?[] { (long)ser.Count, null };
    }
    public static object?[] Decode(GoSlice buf, object order, object? data)
    {
        bool big = ((GoByteOrder)order).Big;
        var ptr = (GoPtr)data!;
        int n = SizeOf(ptr.Value);
        if (buf.Len < n) return new object?[] { 0L, new GoError(GoString.FromDotNetString("binary.Decode: buffer too small")) };
        var slice = new byte[n];
        for (int i = 0; i < n; i++) slice[i] = (byte)System.Convert.ToInt64(buf.Data![buf.Off + i]);
        ptr.Value = Deser(ptr.Value, slice, big);
        return new object?[] { (long)n, null };
    }

    private static void Emit(System.Collections.Generic.List<byte> outp, bool big, byte[] le)
    {
        if (big) System.Array.Reverse(le);
        outp.AddRange(le);
    }

    private static void Ser(System.Collections.Generic.List<byte> outp, bool big, object? v)
    {
        switch (v)
        {
            case bool b: outp.Add((byte)(b ? 1 : 0)); break;
            case byte by: outp.Add(by); break;
            case sbyte sb: outp.Add((byte)sb); break;
            case short s: Emit(outp, big, System.BitConverter.GetBytes(s)); break;
            case ushort us: Emit(outp, big, System.BitConverter.GetBytes(us)); break;
            case int i: Emit(outp, big, System.BitConverter.GetBytes(i)); break;
            case uint ui: Emit(outp, big, System.BitConverter.GetBytes(ui)); break;
            case long l: Emit(outp, big, System.BitConverter.GetBytes(l)); break;
            case ulong ul: Emit(outp, big, System.BitConverter.GetBytes(ul)); break;
            case float f: Emit(outp, big, System.BitConverter.GetBytes(f)); break;
            case double d: Emit(outp, big, System.BitConverter.GetBytes(d)); break;
            case GoSlice sl: for (int k = 0; k < sl.Len; k++) Ser(outp, big, sl.Data![sl.Off + k]); break;
            case GoPtr p: Ser(outp, big, p.Value); break;
            default:
                // struct: serialize public fields in order.
                if (v != null)
                    foreach (var fi in v.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        Ser(outp, big, fi.GetValue(v));
                break;
        }
    }

    // binary.Write(w io.Writer, order ByteOrder, data any) error
    public static object? Write(object? w, object order, object? data)
    {
        var outp = new System.Collections.Generic.List<byte>();
        Ser(outp, ((GoByteOrder)order).Big, data);
        Compress.WriteRaw(w, outp.ToArray());
        return null;
    }

    private static int SizeOf(object? v) => v switch
    {
        bool or byte or sbyte => 1, short or ushort => 2, int or uint or float => 4,
        long or ulong or double => 8, _ => 0,
    };
    private static object Deser(object? proto, byte[] b, bool big)
    {
        if (big) System.Array.Reverse(b);
        return proto switch
        {
            short => System.BitConverter.ToInt16(b), ushort => System.BitConverter.ToUInt16(b),
            int => System.BitConverter.ToInt32(b), uint => System.BitConverter.ToUInt32(b),
            long => System.BitConverter.ToInt64(b), ulong => System.BitConverter.ToUInt64(b),
            float => System.BitConverter.ToSingle(b), double => System.BitConverter.ToDouble(b),
            byte => b[0], sbyte => (sbyte)b[0], bool => b[0] != 0, _ => 0,
        };
    }

    // binary.Read(r io.Reader, order ByteOrder, data any) error — data is a pointer.
    public static object? Read(object? r, object order, object? data)
    {
        bool big = ((GoByteOrder)order).Big;
        var ptr = (GoPtr)data!;
        int n = SizeOf(ptr.Value);
        byte[] bytes = r is GoBuffer ? BytesBuffer.ReadRaw(r, n) : Readers.Drain(r);
        if (bytes.Length < n) return new GoError(GoString.FromDotNetString("unexpected EOF"));
        var slice = new byte[n];
        System.Array.Copy(bytes, slice, n);
        ptr.Value = Deser(ptr.Value, slice, big);
        return null;
    }

    // binary.Size(data any) int — byte count, or -1 if not fixed-size.
    public static long Size(object? data)
    {
        var outp = new System.Collections.Generic.List<byte>();
        Ser(outp, false, data);
        return outp.Count;
    }

    private static byte Get(GoSlice b, int i) => (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
    private static void Set(GoSlice b, int i, byte v) => b.Data![b.Off + i] = (int)v;

    // readOne reads a single byte from any io.ByteReader. Known shim reader types
    // are read directly; a user reader is driven through the callback bridge.
    // Returns the byte in [0,255], or -1 with err set on EOF/error.
    private static int ReadOne(object? r, out object? err)
    {
        object?[]? res = r switch
        {
            GoReader => Readers.Reader_ReadByte(r),
            GoBuffer => BytesBuffer.ReadByte(r),
            not null when Bridge.HasMethod(r, "ReadByte") => Bridge.CallMethod(r, "ReadByte", System.Array.Empty<object?>()) as object?[],
            _ => null,
        };
        if (res == null) { err = Io.EOFSentinel; return -1; }
        err = res.Length > 1 ? res[1] : null;
        int b = res.Length > 0 && res[0] != null ? (int)System.Convert.ToInt64(res[0]) : 0;
        if (err != null) return -1;
        return b & 0xff;
    }

    private static readonly GoError ErrOverflow = new(GoString.FromDotNetString("binary: varint overflows a 64-bit integer"));

    // ReadUvarint mirrors encoding/binary.ReadUvarint, reading LEB128 from an io.ByteReader.
    public static object?[] ReadUvarint(object? r)
    {
        ulong x = 0; int s = 0;
        for (int i = 0; ; i++)
        {
            int b = ReadOne(r, out var err);
            if (err != null) return new object?[] { x, err };
            if (b < 0x80)
            {
                if (i == 9 && b > 1) return new object?[] { (ulong)0, ErrOverflow };
                return new object?[] { x | ((ulong)b << s), null };
            }
            x |= (ulong)(b & 0x7f) << s;
            s += 7;
        }
    }

    // ReadVarint mirrors encoding/binary.ReadVarint (zig-zag decode over ReadUvarint).
    public static object?[] ReadVarint(object? r)
    {
        var ux = ReadUvarint(r);
        ulong u = System.Convert.ToUInt64(ux[0] ?? (ulong)0);
        long x = (long)(u >> 1);
        if ((u & 1) != 0) x = ~x;
        return new object?[] { x, ux[1] };
    }

    public static uint Uint16(object o, GoSlice b)
    {
        var e = (GoByteOrder)o;
        return (uint)(e.Big ? (Get(b, 0) << 8) | Get(b, 1) : Get(b, 0) | (Get(b, 1) << 8));
    }
    public static uint Uint32(object o, GoSlice b)
    {
        var e = (GoByteOrder)o; uint r = 0;
        for (int i = 0; i < 4; i++) r |= (uint)Get(b, i) << (e.Big ? (3 - i) * 8 : i * 8);
        return r;
    }
    public static ulong Uint64(object o, GoSlice b)
    {
        var e = (GoByteOrder)o; ulong r = 0;
        for (int i = 0; i < 8; i++) r |= (ulong)Get(b, i) << (e.Big ? (7 - i) * 8 : i * 8);
        return r;
    }
    public static void PutUint16(object o, GoSlice b, uint v)
    {
        var e = (GoByteOrder)o;
        if (e.Big) { Set(b, 0, (byte)(v >> 8)); Set(b, 1, (byte)v); } else { Set(b, 0, (byte)v); Set(b, 1, (byte)(v >> 8)); }
    }
    public static void PutUint32(object o, GoSlice b, uint v)
    {
        var e = (GoByteOrder)o;
        for (int i = 0; i < 4; i++) Set(b, i, (byte)(v >> (e.Big ? (3 - i) * 8 : i * 8)));
    }
    public static void PutUint64(object o, GoSlice b, ulong v)
    {
        var e = (GoByteOrder)o;
        for (int i = 0; i < 8; i++) Set(b, i, (byte)(v >> (e.Big ? (7 - i) * 8 : i * 8)));
    }

    // binary.ByteOrder.AppendUint16/32/64 (Go 1.19 AppendByteOrder): append the value's
    // bytes (in the order's endianness) to buf and return the grown slice.
    public static GoSlice AppendUint16(object o, GoSlice buf, uint v)
    {
        bool big = ((GoByteOrder)o).Big;
        var add = new System.Collections.Generic.List<byte>(2);
        for (int i = 0; i < 2; i++) add.Add((byte)(v >> (big ? (1 - i) * 8 : i * 8)));
        return AppendBytes(buf, add);
    }
    public static GoSlice AppendUint32(object o, GoSlice buf, uint v)
    {
        bool big = ((GoByteOrder)o).Big;
        var add = new System.Collections.Generic.List<byte>(4);
        for (int i = 0; i < 4; i++) add.Add((byte)(v >> (big ? (3 - i) * 8 : i * 8)));
        return AppendBytes(buf, add);
    }
    public static GoSlice AppendUint64(object o, GoSlice buf, ulong v)
    {
        bool big = ((GoByteOrder)o).Big;
        var add = new System.Collections.Generic.List<byte>(8);
        for (int i = 0; i < 8; i++) add.Add((byte)(v >> (big ? (7 - i) * 8 : i * 8)));
        return AppendBytes(buf, add);
    }
}
