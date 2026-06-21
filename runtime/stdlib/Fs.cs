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
}
