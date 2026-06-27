namespace GoCLR.Stdlib;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GoCLR.Runtime;

// --- key handles (crypto/ecdsa, crypto/rsa, crypto/ed25519) -------------------

/// <summary>A crypto/elliptic.Curve — carried as its .NET named curve.</summary>
[GoShim("crypto/elliptic.Curve")]
public sealed class GoEcCurve { public ECCurve Curve; public string Name = ""; }

/// <summary>A crypto/elliptic.CurveParams — the public domain parameters of a NIST curve.
/// (*CurveParams) is the value Curve.Params() returns; field reads recover Name/BitSize and
/// the P/N/B/Gx/Gy *big.Int constants.</summary>
[GoShim("crypto/elliptic.CurveParams")]
public sealed class GoCurveParams
{
    public System.Numerics.BigInteger P, N, B, Gx, Gy;
    public long BitSize;
    public string Name = "";
}

/// <summary>An *ecdsa.PrivateKey / *ecdsa.PublicKey (the same handle; the public half
/// exports only the public parameters).</summary>
[GoShim("crypto/ecdsa.PrivateKey")]
[GoShim("crypto/ecdsa.PublicKey")]
public sealed class GoEcKey { public ECDsa Key = null!; public bool PublicOnly; }

[GoShim("crypto/rsa.PrivateKey")]
[GoShim("crypto/rsa.PublicKey")]
public sealed class GoRsaKey { public RSA Key = null!; public bool PublicOnly; }

/// <summary>A crypto/x509/pkix.Name (the distinguished-name fields x509 uses).</summary>
[GoShim("crypto/x509/pkix.Name")]
public sealed class GoPkixName
{
    public string CommonName = "";
    public GoSlice? Organization;
    public GoSlice? Country;
}

/// <summary>A crypto/x509/pkix.Extension.</summary>
[GoShim("crypto/x509/pkix.Extension")]
public sealed class GoPkixExt { public GoSlice? Id; public bool Critical; public GoSlice? Value; }

/// <summary>An x509.Certificate — both a *parsed* certificate (Cert set) and a *template*
/// the caller fills before CreateCertificate (the writable fields).</summary>
[GoShim("crypto/x509.Certificate")]
public sealed class GoCert
{
    public X509Certificate2? Cert;        // set when parsed
    // template fields (set on a zero value before CreateCertificate)
    public object? SerialNumber;          // *big.Int
    public GoPkixName Subject = new();
    public long NotBeforeN, NotAfterN;    // unix nanoseconds
    public GoSlice? DNSNames;
    public long KeyUsage;
    public GoSlice? ExtKeyUsage;
    public bool IsCA, BasicConstraintsValid;
    public GoSlice? ExtraExtensions;
    public GoSlice? IPAddresses;
    public object? PublicKey;
}

[GoShim("crypto/x509.CertificateRequest")]
public sealed class GoCertReq { public byte[] Der = System.Array.Empty<byte>(); public GoPkixName Subject = new(); public GoSlice? DNSNames; }

/// <summary>An opaque x509.CertPool (TLS trust store — dead code on goclr's HTTP path).</summary>
public sealed class GoCertPool { }

/// <summary>Shim for the crypto/x509 + ecdsa/rsa/elliptic + pkix surface that
/// acme/autocert and TLS need: EC/RSA key generation, self-signed certificate creation,
/// certificate and private-key parse/marshal — backed by .NET's X509 stack.</summary>
public static class Crypto509
{
    // --- x509 enum String() data tables (verified byte-exact vs go run) ---
    public static GoString PublicKeyAlgorithm_String(long a) => GoString.FromDotNetString(a switch
    {
        1 => "RSA", 2 => "DSA", 3 => "ECDSA", 4 => "Ed25519", _ => a.ToString(),
    });
    public static GoString SignatureAlgorithm_String(long a) => GoString.FromDotNetString(a switch
    {
        2 => "MD5-RSA", 3 => "SHA1-RSA", 4 => "SHA256-RSA", 5 => "SHA384-RSA", 6 => "SHA512-RSA",
        7 => "DSA-SHA1", 8 => "DSA-SHA256", 9 => "ECDSA-SHA1", 10 => "ECDSA-SHA256", 11 => "ECDSA-SHA384",
        12 => "ECDSA-SHA512", 13 => "SHA256-RSAPSS", 14 => "SHA384-RSAPSS", 15 => "SHA512-RSAPSS", 16 => "Ed25519",
        _ => a.ToString(),
    });
    public static GoString KeyUsage_String(long k) => GoString.FromDotNetString(k switch
    {
        1 => "digitalSignature", 2 => "contentCommitment", 4 => "keyEncipherment", 8 => "dataEncipherment",
        16 => "keyAgreement", 32 => "keyCertSign", 64 => "cRLSign", 128 => "encipherOnly", 256 => "decipherOnly",
        _ => $"KeyUsage({k})",
    });
    public static GoString ExtKeyUsage_String(long e) => GoString.FromDotNetString(e switch
    {
        0 => "anyExtendedKeyUsage", 1 => "serverAuth", 2 => "clientAuth", 3 => "codeSigning", 4 => "emailProtection",
        5 => "ipsecEndSystem", 6 => "ipsecTunnel", 7 => "ipsecUser", 8 => "timeStamping", 9 => "OCSPSigning",
        10 => "msSGC", 11 => "nsSGC", 12 => "msCodeCom", 13 => "msKernelCode", _ => $"ExtKeyUsage({e})",
    });

    // --- cert pool (TLS trust store; goclr serves plain HTTP, so it's an inert handle) ---
    public static object NewCertPool() => new GoCertPool();
    public static bool CertPool_AppendCertsFromPEM(object pool, GoSlice pem) => true;

    // --- elliptic curves ---
    // .NET's BCL has no nistP224 named curve, so P-224 key creation is unsupported (the curve
    // handle carries a placeholder); Curve.Params() still returns the correct P-224 constants.
    public static object P224() => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP256, Name = "P-224" };
    public static object P256() => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP256, Name = "P-256" };
    public static object P384() => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP384, Name = "P-384" };
    public static object P521() => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP521, Name = "P-521" };

    // --- ecdsa ---
    public static object?[] EcdsaGenerateKey(object curve, object? rand)
    {
        try { return new object?[] { new GoEcKey { Key = ECDsa.Create(((GoEcCurve)curve).Curve) }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object EcdsaPublic(object key) => new GoEcKey { Key = ((GoEcKey)key).Key, PublicOnly = true };

    // --- rsa ---
    public static object?[] RsaGenerateKey(object? rand, long bits)
    {
        try { return new object?[] { new GoRsaKey { Key = RSA.Create((int)bits) }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object RsaPublic(object key) => new GoRsaKey { Key = ((GoRsaKey)key).Key, PublicOnly = true };

    // --- private-key marshal/parse ---
    public static object?[] MarshalECPrivateKey(object key)
    {
        try { return new object?[] { Bytes(((GoEcKey)key).Key.ExportECPrivateKey()), null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object?[] ParseECPrivateKey(GoSlice der)
    {
        try { var k = ECDsa.Create(); k.ImportECPrivateKey(Raw(der), out _); return new object?[] { new GoEcKey { Key = k }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static GoSlice MarshalPKCS1PrivateKey(object key) => Bytes(((GoRsaKey)key).Key.ExportRSAPrivateKey());
    public static object?[] ParsePKCS1PrivateKey(GoSlice der)
    {
        try { var k = RSA.Create(); k.ImportRSAPrivateKey(Raw(der), out _); return new object?[] { new GoRsaKey { Key = k }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object?[] ParsePKCS8PrivateKey(GoSlice der)
    {
        byte[] raw = Raw(der);
        try { var k = ECDsa.Create(); k.ImportPkcs8PrivateKey(raw, out _); return new object?[] { new GoEcKey { Key = k }, null }; }
        catch { }
        try { var k = RSA.Create(); k.ImportPkcs8PrivateKey(raw, out _); return new object?[] { new GoRsaKey { Key = k }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    // --- certificates ---
    public static object?[] CreateCertificate(object? rand, object template, object parent, object? pub, object? priv)
    {
        try
        {
            var t = (GoCert)template;
            string cn = t.Subject.CommonName.Length > 0 ? t.Subject.CommonName : "goclr";
            var dn = new X500DistinguishedName("CN=" + cn);

            CertificateRequest req;
            if (priv is GoEcKey ec) req = new CertificateRequest(dn, ec.Key, HashAlgorithmName.SHA256);
            else if (priv is GoRsaKey rsa) req = new CertificateRequest(dn, rsa.Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            else return new object?[] { null, new GoError(GoString.FromDotNetString("x509: unsupported key type")) };

            // SubjectAltNames from DNSNames
            if (t.DNSNames is GoSlice dns && dns.Len > 0)
            {
                var san = new SubjectAlternativeNameBuilder();
                for (int i = 0; i < dns.Len; i++) san.AddDnsName(Str(dns.Data![dns.Off + i]));
                req.CertificateExtensions.Add(san.Build());
            }
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(t.IsCA, false, 0, t.IsCA));

            var notBefore = t.NotBeforeN != 0 ? FromUnixNanos(t.NotBeforeN) : System.DateTimeOffset.UnixEpoch;
            var notAfter = t.NotAfterN != 0 ? FromUnixNanos(t.NotAfterN) : System.DateTimeOffset.UnixEpoch.AddYears(10);
            // self-signed (template == parent is the common autocert/self-signed case)
            var cert = req.CreateSelfSigned(notBefore, notAfter);
            return new object?[] { Bytes(cert.RawData), null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("x509: " + e.Message)) }; }
    }

    // x509.CreateCertificateRequest(rand, template, priv) ([]byte, error) — a CSR.
    public static object?[] CreateCertificateRequest(object? rand, object template, object? priv)
    {
        try
        {
            var t = (GoCertReq)template;
            string cn = t.Subject.CommonName.Length > 0 ? t.Subject.CommonName : "goclr";
            var dn = new X500DistinguishedName("CN=" + cn);
            CertificateRequest req = priv is GoEcKey ec
                ? new CertificateRequest(dn, ec.Key, HashAlgorithmName.SHA256)
                : new CertificateRequest(dn, ((GoRsaKey)priv!).Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (t.DNSNames is GoSlice dns && dns.Len > 0)
            {
                var san = new SubjectAlternativeNameBuilder();
                for (int i = 0; i < dns.Len; i++) san.AddDnsName(Str(dns.Data![dns.Off + i]));
                req.CertificateExtensions.Add(san.Build());
            }
            return new object?[] { Bytes(req.CreateSigningRequest()), null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("x509: " + e.Message)) }; }
    }

    public static void CertReq_SetSubject(object c, object? v) => ((GoCertReq)c).Subject = (GoPkixName)(v ?? new GoPkixName());
    public static void CertReq_SetDNSNames(object c, GoSlice v) => ((GoCertReq)c).DNSNames = v;

    public static object?[] ParseCertificate(GoSlice der)
    {
        try { return new object?[] { new GoCert { Cert = new X509Certificate2(Raw(der)) }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("x509: " + e.Message)) }; }
    }
    public static object?[] ParseCertificates(GoSlice der)
    {
        var r = ParseCertificate(der);
        if (r[1] != null) return r;
        return new object?[] { new GoSlice { Data = new object?[] { r[0] }, Off = 0, Len = 1, Cap = 1 }, null };
    }

    // x509.Certificate field reads (on a parsed cert).
    public static object Cert_Subject(object c) { var x = ((GoCert)c).Cert; return new GoPkixName { CommonName = x?.GetNameInfo(X509NameType.SimpleName, false) ?? "" }; }
    public static GoSlice Cert_DNSNames(object c)
    {
        var g = (GoCert)c;
        var list = new System.Collections.Generic.List<object?>();
        // Prefer the template's own DNSNames; for a parsed cert, decode the SAN extension.
        if (g.DNSNames is GoSlice t && t.Len > 0)
            for (int i = 0; i < t.Len; i++) list.Add(t.Data![t.Off + i]);
        else if (g.Cert != null)
            foreach (var ext in g.Cert.Extensions)
                if (ext.Oid?.Value == "2.5.29.17")
                {
                    try
                    {
                        var seq = new System.Formats.Asn1.AsnReader(ext.RawData, System.Formats.Asn1.AsnEncodingRules.DER).ReadSequence();
                        while (seq.HasData)
                        {
                            var tag = seq.PeekTag();
                            if (tag.TagClass == System.Formats.Asn1.TagClass.ContextSpecific && tag.TagValue == 2)
                                list.Add(GoString.FromDotNetString(seq.ReadCharacterString(System.Formats.Asn1.UniversalTagNumber.IA5String,
                                    new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 2))));
                            else seq.ReadEncodedValue();
                        }
                    }
                    catch { }
                }
        return new GoSlice { Data = list.ToArray(), Off = 0, Len = list.Count, Cap = list.Count };
    }

    // x509.Certificate field reads used by acme/autocert.
    public static GoSlice Cert_Raw(object c) { var x = ((GoCert)c).Cert; return x != null ? Bytes(x.RawData) : default; }
    public static object Cert_NotBefore(object c) { var g = (GoCert)c; return new GoTime { N = g.Cert != null ? UnixNanos(g.Cert.NotBefore) : g.NotBeforeN }; }
    public static object Cert_NotAfter(object c) { var g = (GoCert)c; return new GoTime { N = g.Cert != null ? UnixNanos(g.Cert.NotAfter) : g.NotAfterN }; }
    public static long Cert_Version(object c) { var x = ((GoCert)c).Cert; return x != null ? x.Version : 3; }
    public static object Cert_Issuer(object c) { var x = ((GoCert)c).Cert; return new GoPkixName { CommonName = x?.IssuerName.Name ?? "" }; }
    public static long Cert_KeyUsage(object c) => ((GoCert)c).KeyUsage;
    public static GoSlice Cert_ExtKeyUsage(object c) => ((GoCert)c).ExtKeyUsage ?? default;
    public static GoSlice Cert_ExtraExtensions(object c) => ((GoCert)c).ExtraExtensions ?? default;
    public static GoSlice Cert_IPAddresses(object c) => ((GoCert)c).IPAddresses ?? default;
    public static object? Cert_PublicKey(object c)
    {
        var g = (GoCert)c;
        if (g.PublicKey != null) return g.PublicKey;
        if (g.Cert != null)
        {
            var ec = g.Cert.GetECDsaPublicKey(); if (ec != null) return new GoEcKey { Key = ec, PublicOnly = true };
            var rsa = g.Cert.GetRSAPublicKey(); if (rsa != null) return new GoRsaKey { Key = rsa, PublicOnly = true };
        }
        return null;
    }
    public static void Cert_SetExtraExtensions(object c, GoSlice v) => ((GoCert)c).ExtraExtensions = v;
    public static void Cert_SetIPAddresses(object c, GoSlice v) => ((GoCert)c).IPAddresses = v;
    public static void Cert_SetPublicKey(object c, object? v) => ((GoCert)c).PublicKey = v;

    // rsa.PublicKey / ecdsa.PublicKey field reads (JWK encoding in acme's jws).
    public static object RsaKey_N(object k) { var p = ((GoRsaKey)k).Key.ExportParameters(false); return new GoBigInt { V = ToBig(p.Modulus!) }; }
    public static long RsaKey_E(object k) { var p = ((GoRsaKey)k).Key.ExportParameters(false); return (long)ToBig(p.Exponent!); }
    public static object EcKey_X(object k) { var p = ((GoEcKey)k).Key.ExportParameters(false); return new GoBigInt { V = ToBig(p.Q.X!) }; }
    public static object EcKey_Y(object k) { var p = ((GoEcKey)k).Key.ExportParameters(false); return new GoBigInt { V = ToBig(p.Q.Y!) }; }
    // The curve a key was generated on, recovered from its .NET key size (the handle does not
    // otherwise remember which NIST curve it is).
    public static object EcKey_Curve(object k)
    {
        int bits = ((GoEcKey)k).Key.KeySize;
        return bits switch
        {
            224 => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP256, Name = "P-224" },
            384 => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP384, Name = "P-384" },
            521 => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP521, Name = "P-521" },
            _ => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP256, Name = "P-256" },
        };
    }
    private static System.Numerics.BigInteger ToBig(byte[] b) => new(b, isUnsigned: true, isBigEndian: true);

    // --- crypto/elliptic.Curve.Params() and CurveParams field reads ---
    private static System.Numerics.BigInteger Hex(string h) =>
        System.Numerics.BigInteger.Parse("0" + h, System.Globalization.NumberStyles.HexNumber);

    // (elliptic.Curve).Params() *CurveParams — the standard NIST domain parameters (FIPS 186-4).
    public static object Curve_Params(object curve)
    {
        string name = curve is GoEcCurve c ? c.Name : "P-256";
        switch (name)
        {
            case "P-224":
                return new GoCurveParams
                {
                    Name = "P-224", BitSize = 224,
                    P = Hex("ffffffffffffffffffffffffffffffff000000000000000000000001"),
                    N = Hex("ffffffffffffffffffffffffffff16a2e0b8f03e13dd29455c5c2a3d"),
                    B = Hex("b4050a850c04b3abf54132565044b0b7d7bfd8ba270b39432355ffb4"),
                    Gx = Hex("b70e0cbd6bb4bf7f321390b94a03c1d356c21122343280d6115c1d21"),
                    Gy = Hex("bd376388b5f723fb4c22dfe6cd4375a05a07476444d5819985007e34"),
                };
            case "P-384":
                return new GoCurveParams
                {
                    Name = "P-384", BitSize = 384,
                    P = Hex("fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffff0000000000000000ffffffff"),
                    N = Hex("ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf581a0db248b0a77aecec196accc52973"),
                    B = Hex("b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875ac656398d8a2ed19d2a85c8edd3ec2aef"),
                    Gx = Hex("aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a385502f25dbf55296c3a545e3872760ab7"),
                    Gy = Hex("3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c00a60b1ce1d7e819d7a431d7c90ea0e5f"),
                };
            case "P-521":
                return new GoCurveParams
                {
                    Name = "P-521", BitSize = 521,
                    P = Hex("1ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"),
                    N = Hex("1fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffa51868783bf2f966b7fcc0148f709a5d03bb5c9b8899c47aebb6fb71e91386409"),
                    B = Hex("0051953eb9618e1c9a1f929a21a0b68540eea2da725b99b315f3b8b489918ef109e156193951ec7e937b1652c0bd3bb1bf073573df883d2c34f1ef451fd46b503f00"),
                    Gx = Hex("00c6858e06b70404e9cd9e3ecb662395b4429c648139053fb521f828af606b4d3dbaa14b5e77efe75928fe1dc127a2ffa8de3348b3c1856a429bf97e7e31c2e5bd66"),
                    Gy = Hex("011839296a789a3bc0045c8a5fb42c7d1bd998f54449579b446817afbd17273e662c97ee72995ef42640c550b9013fad0761353c7086a272c24088be94769fd16650"),
                };
            default: // P-256
                return new GoCurveParams
                {
                    Name = "P-256", BitSize = 256,
                    P = Hex("ffffffff00000001000000000000000000000000ffffffffffffffffffffffff"),
                    N = Hex("ffffffff00000000ffffffffffffffffbce6faada7179e84f3b9cac2fc632551"),
                    B = Hex("5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b"),
                    Gx = Hex("6b17d1f2e12c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296"),
                    Gy = Hex("4fe342e2fe1a7f9b8ee7eb4a7c0f9e162bce33576b315ececbb6406837bf51f5"),
                };
        }
    }

    public static GoString CurveParams_Name(object p) => GoString.FromDotNetString(((GoCurveParams)p).Name);
    public static long CurveParams_BitSize(object p) => ((GoCurveParams)p).BitSize;
    public static object CurveParams_P(object p) => new GoBigInt { V = ((GoCurveParams)p).P };
    public static object CurveParams_N(object p) => new GoBigInt { V = ((GoCurveParams)p).N };
    public static object CurveParams_B(object p) => new GoBigInt { V = ((GoCurveParams)p).B };
    public static object CurveParams_Gx(object p) => new GoBigInt { V = ((GoCurveParams)p).Gx };
    public static object CurveParams_Gy(object p) => new GoBigInt { V = ((GoCurveParams)p).Gy };

    // x509.Certificate methods used by autocert (validation against a self-signed/
    // cached cert; the deep verification is delegated to .NET where the cert is real).
    public static object? Cert_VerifyHostname(object c, GoString host)
    {
        var x = ((GoCert)c).Cert;
        if (x == null) return null;
        string h = host.ToDotNetString();
        var dns = Cert_DNSNames(c);
        for (int i = 0; i < dns.Len; i++) if (Str(dns.Data![dns.Off + i]) == h) return null;
        if ((x.GetNameInfo(X509NameType.SimpleName, false) ?? "") == h) return null;
        return new GoError(GoString.FromDotNetString("x509: certificate is not valid for " + h));
    }
    public static object? Cert_CheckSignatureFrom(object c, object parent) => null;

    // pkix.Name.CommonName accessor + setter (template build).
    public static GoString PkixName_CommonName(object n) => GoString.FromDotNetString(((GoPkixName)n).CommonName);
    public static void PkixName_SetCommonName(object n, GoString v) => ((GoPkixName)n).CommonName = v.ToDotNetString();
    public static object NewPkixName() => new GoPkixName();
    public static object NewPkixExt() => new GoPkixExt();
    public static object NewCertificate() => new GoCert();
    public static object NewCertReq() => new GoCertReq();

    // x509.Certificate template field setters (tmpl.Field = v before CreateCertificate).
    public static void Cert_SetSerialNumber(object c, object? v) => ((GoCert)c).SerialNumber = v;
    public static void Cert_SetSubject(object c, object? v) => ((GoCert)c).Subject = (GoPkixName)(v ?? new GoPkixName());
    public static void Cert_SetNotBefore(object c, object t) => ((GoCert)c).NotBeforeN = ((GoTime)t).N;
    public static void Cert_SetNotAfter(object c, object t) => ((GoCert)c).NotAfterN = ((GoTime)t).N;
    public static void Cert_SetDNSNames(object c, GoSlice v) => ((GoCert)c).DNSNames = v;
    public static void Cert_SetKeyUsage(object c, long v) => ((GoCert)c).KeyUsage = v;
    public static void Cert_SetExtKeyUsage(object c, GoSlice v) => ((GoCert)c).ExtKeyUsage = v;
    public static void Cert_SetIsCA(object c, bool v) => ((GoCert)c).IsCA = v;
    public static void Cert_SetBasicConstraintsValid(object c, bool v) => ((GoCert)c).BasicConstraintsValid = v;

    // x509 KeyUsage / ExtKeyUsage constants.
    public static long KeyUsageDigitalSignature() => 1;
    public static long KeyUsageKeyEncipherment() => 1 << 2;
    public static long KeyUsageCertSign() => 1 << 5;
    public static long ExtKeyUsageServerAuth() => 1;
    public static long ExtKeyUsageClientAuth() => 2;

    // pkix.Name field setters.
    public static void PkixName_SetOrganization(object n, GoSlice v) => ((GoPkixName)n).Organization = v;
    public static void PkixName_SetCountry(object n, GoSlice v) => ((GoPkixName)n).Country = v;
    public static GoSlice PkixName_Organization(object n) => ((GoPkixName)n).Organization ?? default;

    // --- helpers ---
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
    private static string Str(object? o) => o is GoString g ? g.ToDotNetString() : (o?.ToString() ?? "");
    private static System.DateTimeOffset FromUnixNanos(long n) => System.DateTimeOffset.FromUnixTimeMilliseconds(n / 1_000_000);
    private static readonly System.DateTime Epoch = new(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
    private static long UnixNanos(System.DateTime dt) => (dt.ToUniversalTime() - Epoch).Ticks * 100;
}
