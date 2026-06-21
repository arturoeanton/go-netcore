namespace GoCLR.Stdlib;

using System;
using System.Security.Cryptography;
using GoCLR.Runtime;

/// <summary>An encoding/pem.Block.</summary>
[GoShim("encoding/pem.Block")]
public sealed class GoPemBlock
{
    public string Type = "";
    public GoSlice Bytes;
    public GoMap? Headers;
}

/// <summary>Shim for encoding/pem over System.Security.Cryptography.PemEncoding.</summary>
public static class Pem
{
    public static object NewBlock() => new GoPemBlock();
    public static GoString Block_Type(object b) => GoString.FromDotNetString(((GoPemBlock)b).Type);
    public static GoSlice Block_Bytes(object b) => ((GoPemBlock)b).Bytes;
    public static object? Block_Headers(object b) => ((GoPemBlock)b).Headers;
    public static void Block_SetType(object b, GoString v) => ((GoPemBlock)b).Type = v.ToDotNetString();
    public static void Block_SetBytes(object b, GoSlice v) => ((GoPemBlock)b).Bytes = v;
    public static void Block_SetHeaders(object b, object? v) => ((GoPemBlock)b).Headers = v as GoMap;

    // pem.Decode(data) (*Block, rest []byte) — parse the first PEM block; rest is the
    // input after it. Returns (nil, data) when there is no block.
    public static object?[] Decode(GoSlice data)
    {
        byte[] raw = Raw(data);
        var text = System.Text.Encoding.ASCII.GetString(raw).AsSpan();
        if (PemEncoding.TryFind(text, out var f))
        {
            string label = text[f.Label].ToString();
            byte[] der;
            try { der = Convert.FromBase64String(text[f.Base64Data].ToString()); }
            catch { return new object?[] { null, data }; }
            var block = new GoPemBlock { Type = label, Bytes = Bytes(der) };
            int end = f.Location.End.GetOffset(text.Length);
            int restLen = raw.Length - end;
            var rest = new byte[restLen < 0 ? 0 : restLen];
            if (restLen > 0) System.Array.Copy(raw, end, rest, 0, restLen);
            return new object?[] { block, Bytes(rest) };
        }
        return new object?[] { null, data };
    }

    // pem.EncodeToMemory(b) []byte.
    public static GoSlice EncodeToMemory(object b)
    {
        var blk = (GoPemBlock)b;
        char[] pem = PemEncoding.Write(blk.Type, Raw(blk.Bytes));
        return Bytes(System.Text.Encoding.ASCII.GetBytes(pem));
    }

    // pem.Encode(w, b) error — write the PEM to an io.Writer.
    public static object? Encode(object? w, object b)
    {
        Fmt.WriteTo(w, System.Text.Encoding.ASCII.GetString(Raw(EncodeToMemory(b))));
        return null;
    }

    private static byte[] Raw(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
}
