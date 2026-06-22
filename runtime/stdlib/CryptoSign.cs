namespace GoCLR.Stdlib;

using GoCLR.Runtime;

// Fail-closed stubs for the asymmetric sign/verify functions golang-jwt compiles in (ECDSA,
// RSA-PKCS1v15, RSA-PSS, Ed25519). goclr's HMAC (HS256/384/512) path is real; these let the
// jwt package compile and make the asymmetric methods REJECT (Verify -> false / an error,
// Sign -> error) rather than accept anything — safe (never validates a bad token), but ES*/
// RS*/PS*/EdDSA are not functional. Real asymmetric crypto is the crypto/tls/rsa/ecdsa item.
public static class CryptoSign
{
    private static GoError NotSupported(string algo) =>
        new GoError(GoString.FromDotNetString("goclr: " + algo + " sign/verify is not implemented (HMAC only)"));

    // ecdsa.Verify(pub, hash, r, s) bool / ecdsa.Sign(rand, priv, hash) (r, s, err)
    public static bool EcdsaVerify(object? pub, GoSlice hash, object? r, object? s) => false;
    public static object?[] EcdsaSign(object? rand, object? priv, GoSlice hash) =>
        new object?[] { null, null, NotSupported("ECDSA") };

    // rsa.VerifyPKCS1v15(pub, hash, hashed, sig) error / SignPKCS1v15(rand, priv, hash, hashed) ([]byte, err)
    public static object VerifyPKCS1v15(object? pub, object? hash, GoSlice hashed, GoSlice sig) => NotSupported("RSA");
    public static object?[] SignPKCS1v15(object? rand, object? priv, object? hash, GoSlice hashed) =>
        new object?[] { default(GoSlice), NotSupported("RSA") };

    // rsa.VerifyPSS(pub, hash, digest, sig, opts) error / SignPSS(rand, priv, hash, digest, opts) ([]byte, err)
    public static object VerifyPSS(object? pub, object? hash, GoSlice digest, GoSlice sig, object? opts) => NotSupported("RSA-PSS");
    public static object?[] SignPSS(object? rand, object? priv, object? hash, GoSlice digest, object? opts) =>
        new object?[] { default(GoSlice), NotSupported("RSA-PSS") };

    // ed25519.Verify(pub, message, sig) bool
    public static bool Ed25519Verify(GoSlice pub, GoSlice message, GoSlice sig) => false;

    // x509.ParsePKIXPublicKey / ParsePKCS1PublicKey (der) (key, err): goclr does not parse
    // these DER public keys (the asymmetric JWT paths that need them are unsupported), so
    // report an error rather than a bogus key.
    public static object?[] ParsePKIXPublicKey(GoSlice der) =>
        new object?[] { null, NotSupported("x509 public-key parsing") };
    public static object?[] ParsePKCS1PublicKey(GoSlice der) =>
        new object?[] { null, NotSupported("x509 public-key parsing") };

    // x509.DecryptPEMBlock(block, password) ([]byte, error): legacy encrypted PEM — unsupported.
    public static object?[] DecryptPEMBlock(object? block, GoSlice password) =>
        new object?[] { default(GoSlice), NotSupported("x509.DecryptPEMBlock") };
}
