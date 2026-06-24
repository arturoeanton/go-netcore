namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A goclr-produced fs.DirEntry (one directory listing entry). Carries the entry
/// name + whether it is a directory + the rooted real path so Info() can stat it. The
/// [GoShim] annotations let it match fs.DirEntry interface dispatch (like GoFileInfo).</summary>
[GoShim("io/fs.DirEntry")]
public sealed class GoDirEntry
{
    public GoString EntryName;
    public bool Dir;
    public string FullPath = "";
}

/// <summary>Shim for io/fs.FileMode methods (the type is a uint32; constants resolve
/// from source). Only the mask/predicate methods are modeled.</summary>
public static class Fs
{
    // fs.DirEntry method set (mirrors os.FileInfo's, routed through interface dispatch).
    public static GoString DirEntry_Name(object de) => ((GoDirEntry)de).EntryName;
    public static bool DirEntry_IsDir(object de) => ((GoDirEntry)de).Dir;
    public static uint DirEntry_Type(object de) => ((GoDirEntry)de).Dir ? (1u << 31) : 0u;
    public static object?[] DirEntry_Info(object de)
    {
        var d = (GoDirEntry)de;
        return Os.Stat(GoString.FromDotNetString(d.FullPath));
    }

    // fs.ReadDir(fsys, name) ([]fs.DirEntry, error): list a directory. os.DirFS reads the
    // real directory; a ReadDirFS fs.FS uses its own ReadDir through the bridge.
    public static object?[] ReadDir(object? fsys, GoString name)
    {
        if (fsys is GoDirFS dfs)
        {
            string dir = System.IO.Path.Combine(dfs.Root, name.ToDotNetString());
            if (!System.IO.Directory.Exists(dir))
                return new object?[] { NilSlice(), Os.ErrNotExistSentinel };
            var entries = new System.Collections.Generic.List<object?>();
            // Go's fs.ReadDir returns entries sorted by name.
            var names = new System.Collections.Generic.List<string>();
            foreach (var p in System.IO.Directory.GetFileSystemEntries(dir)) names.Add(p);
            names.Sort(System.StringComparer.Ordinal);
            foreach (var full in names)
                entries.Add(new GoDirEntry
                {
                    EntryName = GoString.FromDotNetString(System.IO.Path.GetFileName(full)),
                    Dir = System.IO.Directory.Exists(full),
                    FullPath = full,
                });
            return new object?[] { Slice(entries), null };
        }
        // ReadDirFS fast path: the fs.FS has its own ReadDir.
        if (Bridge.HasMethod(fsys, "ReadDir") &&
            Bridge.CallMethod(fsys, "ReadDir", name) is object?[] rd)
            return rd;
        return new object?[] { NilSlice(), Os.ErrNotExistSentinel };
    }

    private static GoSlice NilSlice() => new() { Data = null, Off = 0, Len = 0, Cap = 0 };
    private static GoSlice Slice(System.Collections.Generic.List<object?> items)
    {
        var d = items.ToArray();
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    // The bits that ModeType selects (dir, symlink, device, pipe, socket, ...).
    private const uint ModeType = 0x8000_0000u | 0x4000_0000u | 0x2000_0000u | 0x1000_0000u
        | 0x0800_0000u | 0x0400_0000u | 0x0200_0000u | 0x0100_0000u | 0x0080_0000u;

    // (fs.FileMode).String(): the "drwxrwxrwx"-style mode string, ported from io/fs.fs.go.
    public static GoString Mode_String(uint m)
    {
        const string str = "dalTLDpSugct?";
        var buf = new char[32];
        int w = 0;
        for (int i = 0; i < str.Length; i++)
            if ((m & (1u << (32 - 1 - i))) != 0) buf[w++] = str[i];
        if (w == 0) buf[w++] = '-';
        const string rwx = "rwxrwxrwx";
        for (int i = 0; i < rwx.Length; i++)
            buf[w++] = (m & (1u << (9 - 1 - i))) != 0 ? rwx[i] : '-';
        return GoString.FromDotNetString(new string(buf, 0, w));
    }

    // fs.FormatDirEntry(dir): "<type-string> <name>[/]" (Type has no perm bits, so strip
    // the trailing 9 rwx chars). Supports goclr's own GoDirEntry.
    public static GoString FormatDirEntry(object de)
    {
        string mode = Mode_String(DirEntry_Type(de)).ToDotNetString();
        mode = mode.Substring(0, mode.Length - 9);
        string name = DirEntry_Name(de).ToDotNetString();
        return GoString.FromDotNetString(mode + " " + name + (DirEntry_IsDir(de) ? "/" : ""));
    }

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
        // StatFS fast path (like Go's fs.Stat): if the fs.FS has its own Stat, use it.
        if (Bridge.HasMethod(fsys, "Stat") &&
            Bridge.CallMethod(fsys, "Stat", name) is object?[] statRes)
            return statRes;
        if (!Bridge.HasMethod(fsys, "Open"))
            return new object?[] { null, Os.ErrNotExistSentinel };
        if (Bridge.CallMethod(fsys, "Open", name) is not object?[] opened)
            return new object?[] { null, Os.ErrNotExistSentinel };
        if (opened.Length > 1 && opened[1] != null)
            return new object?[] { null, opened[1] }; // open error
        var file = opened.Length > 0 ? opened[0] : null;
        // os.File (an os.DirFS / os.Open-backed fs.FS) stats to a goclr GoFileInfo.
        if (file is GoFile gf) return Os.File_Stat(gf);
        // A user fs.File: stat it through the bridge (its FileInfo dispatches through the
        // fs.FileInfo interface now), then close it as Go's fs.Stat does.
        if (Bridge.HasMethod(file, "Stat") &&
            Bridge.CallMethod(file, "Stat") is object?[] info)
        {
            if (Bridge.HasMethod(file, "Close")) Bridge.CallMethod(file, "Close");
            return info;
        }
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
