namespace GoCLR.Stdlib;

using System.Formats.Asn1;
using GoCLR.Runtime;

/// <summary>asn1.StructuralError / asn1.SyntaxError as shim types (single Msg field) so
/// user struct literals create these and their Error()/field access resolve.</summary>
[GoShim("encoding/asn1.StructuralError")]
public sealed class GoAsn1StructuralError : IGoError { public string Msg = ""; public GoString Error() => GoString.FromDotNetString("asn1: structure error: " + Msg); }
[GoShim("encoding/asn1.SyntaxError")]
public sealed class GoAsn1SyntaxError : IGoError { public string Msg = ""; public GoString Error() => GoString.FromDotNetString("asn1: syntax error: " + Msg); }

/// <summary>asn1.BitString as a shim type (Bytes []byte + BitLength int).</summary>
[GoShim("encoding/asn1.BitString")]
public sealed class GoAsn1BitString { public GoSlice Bytes; public long BitLength; }

/// <summary>Shim for the slice of encoding/asn1 that crypto/x509 and acme use: DER
/// marshal of the byte-slice / integer / OID forms they emit, over System.Formats.Asn1.</summary>
public static class Asn1
{
    // asn1.Marshal(val any) ([]byte, error).
    public static object?[] Marshal(object? v)
    {
        try
        {
            var w = new AsnWriter(AsnEncodingRules.DER);
            switch (v)
            {
                case GoSlice s: w.WriteOctetString(Raw(s)); break;  // []byte -> OCTET STRING
                case GoString g: w.WriteCharacterString(UniversalTagNumber.PrintableString, g.ToDotNetString()); break;
                case long n: w.WriteInteger(n); break;
                case bool b: w.WriteBoolean(b); break;
                default: return new object?[] { null, new GoError(GoString.FromDotNetString("asn1: unsupported Marshal type")) };
            }
            return new object?[] { Bytes(w.Encode()), null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("asn1: " + e.Message)) }; }
    }

    // asn1.Unmarshal(b, val) (rest []byte, err error). The structured-decode path is not
    // exercised by the plaintext serving path; this returns no remaining bytes and no
    // error, leaving the target unchanged (documented).
    public static object?[] Unmarshal(GoSlice b, object? val) =>
        new object?[] { new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 }, null };

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

    // StructuralError/SyntaxError are made SHIM types (below) so a user's struct literal
    // creates the shim instance and the method receiver resolves — a goclr-native struct's
    // value receiver binds to a program-generated CLR type the shim can't name.
    public static object NewStructuralError() => new GoAsn1StructuralError();
    public static GoString StructuralError_Error(object e) => GoString.FromDotNetString("asn1: structure error: " + ((GoAsn1StructuralError)e).Msg);
    public static GoString StructuralError_Msg(object e) => GoString.FromDotNetString(((GoAsn1StructuralError)e).Msg);
    public static void StructuralError_SetMsg(object e, GoString v) => ((GoAsn1StructuralError)e).Msg = v.ToDotNetString();
    public static object NewSyntaxError() => new GoAsn1SyntaxError();
    public static GoString SyntaxError_Error(object e) => GoString.FromDotNetString("asn1: syntax error: " + ((GoAsn1SyntaxError)e).Msg);
    public static GoString SyntaxError_Msg(object e) => GoString.FromDotNetString(((GoAsn1SyntaxError)e).Msg);
    public static void SyntaxError_SetMsg(object e, GoString v) => ((GoAsn1SyntaxError)e).Msg = v.ToDotNetString();

    // asn1.BitString (shim type): At(i) returns the i-th bit (MSB first), 0 if out of range;
    // RightAlign returns the bits right-aligned into a fresh byte slice.
    public static object NewBitString() => new GoAsn1BitString();
    public static GoSlice BitString_GetBytes(object b) => ((GoAsn1BitString)b).Bytes;
    public static long BitString_GetBitLength(object b) => ((GoAsn1BitString)b).BitLength;
    public static void BitString_SetBytes(object b, GoSlice v) => ((GoAsn1BitString)b).Bytes = v;
    public static void BitString_SetBitLength(object b, long v) => ((GoAsn1BitString)b).BitLength = v;
    private static byte[] RawBytes(GoSlice s)
    {
        if (s.Data == null) return System.Array.Empty<byte>();
        var r = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) r[i] = (byte)System.Convert.ToInt64(s.Data[s.Off + i]);
        return r;
    }
    public static long BitString_At(object bo, long i)
    {
        var b = (GoAsn1BitString)bo;
        if (i < 0 || i >= b.BitLength) return 0;
        var raw = RawBytes(b.Bytes);
        int x = (int)(i / 8), y = (int)(7 - i % 8);
        return (raw[x] >> y) & 1;
    }
    public static GoSlice BitString_RightAlign(object bo)
    {
        var b = (GoAsn1BitString)bo;
        var raw = RawBytes(b.Bytes);
        int shift = (int)((8 - b.BitLength % 8) % 8);
        if (shift == 0 || raw.Length == 0) return b.Bytes;
        var a = new byte[raw.Length];
        a[0] = (byte)(raw[0] >> shift);
        for (int i = 1; i < raw.Length; i++)
        {
            a[i] = (byte)(raw[i - 1] << (8 - shift));
            a[i] |= (byte)(raw[i] >> shift);
        }
        return Bytes(a);
    }

    // asn1.NullBytes = []byte{tagNull, 0} = {5, 0}.
    public static object NullBytes() => new GoSlice { Data = new object?[] { 5, 0 }, Off = 0, Len = 2, Cap = 2 };

    // (asn1.ObjectIdentifier []int).String()/Equal() — dotted decimal, element-wise compare.
    public static GoString OID_String(GoSlice oid)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < oid.Len; i++) { if (i > 0) sb.Append('.'); sb.Append(System.Convert.ToInt64(oid.Data![oid.Off + i])); }
        return GoString.FromDotNetString(sb.ToString());
    }
    public static bool OID_Equal(GoSlice a, GoSlice b)
    {
        if (a.Len != b.Len) return false;
        for (int i = 0; i < a.Len; i++)
            if (System.Convert.ToInt64(a.Data![a.Off + i]) != System.Convert.ToInt64(b.Data![b.Off + i])) return false;
        return true;
    }
}
