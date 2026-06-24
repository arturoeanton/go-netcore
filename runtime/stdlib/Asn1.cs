namespace GoCLR.Stdlib;

using System.Formats.Asn1;
using GoCLR.Runtime;

/// <summary>asn1.StructuralError / asn1.SyntaxError as shim types (single Msg field) so
/// user struct literals create these and their Error()/field access resolve.</summary>
[GoShim("encoding/asn1.StructuralError")]
public sealed class GoAsn1StructuralError { public string Msg = ""; }
[GoShim("encoding/asn1.SyntaxError")]
public sealed class GoAsn1SyntaxError { public string Msg = ""; }

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
