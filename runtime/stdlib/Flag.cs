namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the subset of Go's <c>flag</c> package consumed by libraries that
/// only probe for testing flags. goclr defines no command-line flags, so Lookup always
/// reports the flag as absent.</summary>
public static class Flag
{
    // flag.Lookup(name) *flag.Flag: nil — no flags are registered under goclr. The
    // return type must be GoPtr to match the *flag.Flag pointer signature the caller emits.
    public static GoPtr? Lookup(GoString name) => null;
}
