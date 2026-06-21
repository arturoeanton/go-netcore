namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for io/fs.FileMode methods (the type is a uint32; constants resolve
/// from source). Only the mask/predicate methods are modeled.</summary>
public static class Fs
{
    // The bits that ModeType selects (dir, symlink, device, pipe, socket, ...).
    private const uint ModeType = 0x8000_0000u | 0x4000_0000u | 0x2000_0000u | 0x1000_0000u
        | 0x0800_0000u | 0x0400_0000u | 0x0200_0000u | 0x0100_0000u | 0x0080_0000u;

    public static uint Mode_Type(uint m) => m & ModeType;
    public static bool Mode_IsDir(uint m) => (m & 0x8000_0000u) != 0;
    public static bool Mode_IsRegular(uint m) => (m & ModeType) == 0;
    public static uint Mode_Perm(uint m) => m & 0x1FFu;

    // The package-level helpers below would normally dispatch through the fs.FS interface
    // (calling fsys.Open). goclr's static-file handlers reach them, but plain HTTP/JSON
    // serving never does — they are honest stubs for that unsupported path.

    // fs.Stat(fsys, name) (fs.FileInfo, error): report the file as absent rather than
    // fabricate a descriptor for a filesystem goclr does not traverse.
    public static object?[] Stat(object? fsys, GoString name) =>
        new object?[] { null, Os.ErrNotExistSentinel };

    // fs.Sub(fsys, dir) (fs.FS, error): return the same filesystem handle (no rooting).
    public static object?[] Sub(object? fsys, GoString dir) => new object?[] { fsys, null };

    // fs.ValidPath(name) bool: Go's rule — unrooted, slash-separated, no "." / ".." element.
    public static bool ValidPath(GoString name)
    {
        string n = name.ToDotNetString();
        if (n == ".") return true;
        if (n.Length == 0 || n[0] == '/' || n[n.Length - 1] == '/') return false;
        foreach (var part in n.Split('/'))
            if (part.Length == 0 || part == "." || part == "..") return false;
        return true;
    }
}
