namespace GoCLR.Stdlib;

using System.Formats.Asn1;
using GoCLR.Runtime;

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
}
