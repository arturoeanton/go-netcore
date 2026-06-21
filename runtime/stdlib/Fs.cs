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

    // fs.Stat(fsys, name) (fs.FileInfo, error): open the file through the fs.FS and stat
    // it — the real Go algorithm. The fs.FS / fs.File methods are reached on the caller's
    // concrete types through the interface method-callback bridge (see Bridge.cs), with
    // direct fast paths for goclr's own os.DirFS and os.File handles.
    public static object?[] Stat(object? fsys, GoString name)
    {
        // os.DirFS: a real directory filesystem — stat the rooted real path.
        if (fsys is GoDirFS dfs)
            return Os.Stat(GoString.FromDotNetString(System.IO.Path.Combine(dfs.Root, name.ToDotNetString())));
        if (!Bridge.HasMethod(fsys, "Open"))
            return new object?[] { null, Os.ErrNotExistSentinel };
        if (Bridge.CallMethod(fsys, "Open", name) is not object?[] opened)
            return new object?[] { null, Os.ErrNotExistSentinel };
        if (opened.Length > 1 && opened[1] != null)
            return new object?[] { null, opened[1] }; // open error
        var file = opened.Length > 0 ? opened[0] : null;
        // os.File (an os.DirFS / os.Open-backed fs.FS) stats to a goclr GoFileInfo. A user
        // fs.File returns the program's own FileInfo type, which fs.FileInfo's shim method
        // registry can't dispatch yet (it assumes GoFileInfo) — close it and report
        // not-found rather than crash. (Tracked: this needs fs.FileInfo to go through
        // interface dispatch like net.Listener did.)
        if (file is GoFile gf) return Os.File_Stat(gf);
        if (Bridge.HasMethod(file, "Close")) Bridge.CallMethod(file, "Close");
        return new object?[] { null, Os.ErrNotExistSentinel };
    }

    // fs.Sub(fsys, dir) (fs.FS, error): a filesystem rooted at dir. For os.DirFS this is a
    // re-rooted DirFS; for a general fs.FS the same handle is returned (no path rewriting).
    public static object?[] Sub(object? fsys, GoString dir)
    {
        if (fsys is GoDirFS dfs)
            return new object?[] { new GoDirFS { Root = System.IO.Path.Combine(dfs.Root, dir.ToDotNetString()) }, null };
        return new object?[] { fsys, null };
    }

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
