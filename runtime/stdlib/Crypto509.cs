namespace GoCLR.Stdlib;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GoCLR.Runtime;

// --- key handles (crypto/ecdsa, crypto/rsa, crypto/ed25519) -------------------

/// <summary>A crypto/elliptic.Curve — carried as its .NET named curve.</summary>
[GoShim("crypto/elliptic.Curve")]
public sealed class GoEcCurve { public ECCurve Curve; public string Name = ""; }

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
    // --- cert pool (TLS trust store; goclr serves plain HTTP, so it's an inert handle) ---
    public static object NewCertPool() => new GoCertPool();
    public static bool CertPool_AppendCertsFromPEM(object pool, GoSlice pem) => true;

    // --- elliptic curves ---
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
    public static object EcKey_Curve(object k) => new GoEcCurve { Curve = ECCurve.NamedCurves.nistP256, Name = "P-256" };
    private static System.Numerics.BigInteger ToBig(byte[] b) => new(b, isUnsigned: true, isBigEndian: true);

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
