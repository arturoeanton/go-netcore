namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A net/netip.Addr: a 128-bit address plus a family/zone tag, modelled exactly
/// as Go does (uint128 hi/lo, with Z 0=zero, 4=IPv4, 6=IPv6 and a zone string). Construction
/// and formatting are ported verbatim from src/net/netip/netip.go, so they are byte-exact.</summary>
[GoShim("net/netip.Addr")]
public sealed class GoNetipAddr
{
    public ulong Hi, Lo;
    public int Z;          // 0 = z0 (zero), 4 = z4 (IPv4), 6 = z6 (IPv6)
    public string Zone = "";

    public GoNetipAddr Clone() => new() { Hi = Hi, Lo = Lo, Z = Z, Zone = Zone };
    public bool ValEq(GoNetipAddr o) => Hi == o.Hi && Lo == o.Lo && Z == o.Z && Zone == o.Zone;
}

public static class Netip
{
    private static GoNetipAddr A(object o) => (GoNetipAddr)o;

    // Zero value for `var a netip.Addr` — the invalid address.
    public static object AddrZero() => new GoNetipAddr();

    // ---- constructors --------------------------------------------------------------------
    public static object AddrFrom4(GoSlice b)
    {
        ulong b0 = U8(b, 0), b1 = U8(b, 1), b2 = U8(b, 2), b3 = U8(b, 3);
        return new GoNetipAddr { Hi = 0, Lo = 0xffff00000000UL | (b0 << 24) | (b1 << 16) | (b2 << 8) | b3, Z = 4 };
    }
    public static object AddrFrom16(GoSlice b)
    {
        ulong hi = 0, lo = 0;
        for (int i = 0; i < 8; i++) hi = (hi << 8) | U8(b, i);
        for (int i = 8; i < 16; i++) lo = (lo << 8) | U8(b, i);
        return new GoNetipAddr { Hi = hi, Lo = lo, Z = 6, Zone = "" };
    }
    public static object?[] AddrFromSlice(GoSlice b) => b.Len switch
    {
        4 => new object?[] { AddrFrom4(b), true },
        16 => new object?[] { AddrFrom16(b), true },
        _ => new object?[] { new GoNetipAddr(), false },
    };
    public static object IPv4Unspecified() => AddrFrom4(Zeros(4));
    public static object IPv6Unspecified() => new GoNetipAddr { Z = 6, Zone = "" };
    public static object IPv6Loopback() { var b = Zeros(16); b.Data![15] = 0x01; return AddrFrom16(b); }
    public static object IPv6LinkLocalAllNodes() { var b = Zeros(16); b.Data![0] = 0xff; b.Data![1] = 0x02; b.Data![15] = 0x01; return AddrFrom16(b); }
    public static object IPv6LinkLocalAllRouters() { var b = Zeros(16); b.Data![0] = 0xff; b.Data![1] = 0x02; b.Data![15] = 0x02; return AddrFrom16(b); }

    // ---- predicates ----------------------------------------------------------------------
    public static bool Addr_Is4(object ip) => A(ip).Z == 4;
    public static bool Addr_Is6(object ip) => A(ip).Z != 0 && A(ip).Z != 4;
    public static bool Addr_Is4In6(object ip) { var a = A(ip); return Addr_Is6(ip) && a.Hi == 0 && (a.Lo >> 32) == 0xffff; }
    public static bool Addr_IsValid(object ip) => A(ip).Z != 0;
    public static long Addr_BitLen(object ip) => A(ip).Z switch { 0 => 0, 4 => 32, _ => 128 };
    public static GoString Addr_Zone(object ip) => GoString.FromDotNetString(A(ip).Zone);

    // ---- byte views ----------------------------------------------------------------------
    public static GoSlice Addr_As16(object ip) { var a = A(ip); var b = Zeros(16); for (int i = 0; i < 8; i++) b.Data![i] = (int)((a.Hi >> ((7 - i) * 8)) & 0xff); for (int i = 0; i < 8; i++) b.Data![8 + i] = (int)((a.Lo >> ((7 - i) * 8)) & 0xff); return b; }
    public static GoSlice Addr_As4(object ip)
    {
        var a = A(ip);
        if (a.Z == 4 || Addr_Is4In6(ip)) { var b = Zeros(4); for (int i = 0; i < 4; i++) b.Data![i] = (int)((a.Lo >> ((3 - i) * 8)) & 0xff); return b; }
        throw new GoPanicException(GoString.FromDotNetString(a.Z == 0 ? "As4 called on IP zero value" : "As4 called on IPv6 address"));
    }
    public static GoSlice Addr_AsSlice(object ip)
    {
        var a = A(ip);
        if (a.Z == 0) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
        return a.Z == 4 ? Addr_As4(ip) : Addr_As16(ip);
    }
    public static object Addr_Unmap(object ip) { var a = A(ip); if (Addr_Is4In6(ip)) { var c = a.Clone(); c.Z = 4; return c; } return a; }

    // ---- ordering / iteration ------------------------------------------------------------
    public static long Addr_Compare(object ipo, object ip2o)
    {
        var ip = A(ipo); var ip2 = A(ip2o);
        long f1 = Addr_BitLen(ipo), f2 = Addr_BitLen(ip2o);
        if (f1 < f2) return -1; if (f1 > f2) return 1;
        if (ip.Hi < ip2.Hi) return -1; if (ip.Hi > ip2.Hi) return 1;
        if (ip.Lo < ip2.Lo) return -1; if (ip.Lo > ip2.Lo) return 1;
        if (Addr_Is6(ipo)) { int c = string.CompareOrdinal(ip.Zone, ip2.Zone); if (c < 0) return -1; if (c > 0) return 1; }
        return 0;
    }
    public static bool Addr_Less(object ip, object ip2) => Addr_Compare(ip, ip2) == -1;
    public static object Addr_Next(object ipo)
    {
        var a = A(ipo).Clone(); AddOne(a);
        if (a.Z == 4) { if ((uint)a.Lo == 0) return new GoNetipAddr(); }
        else if (a.Hi == 0 && a.Lo == 0) return new GoNetipAddr();
        return a;
    }
    public static object Addr_Prev(object ipo)
    {
        var a = A(ipo).Clone();
        if (a.Z == 4) { if ((uint)a.Lo == 0) return new GoNetipAddr(); }
        else if (a.Hi == 0 && a.Lo == 0) return new GoNetipAddr();
        SubOne(a); return a;
    }

    // ---- classification ------------------------------------------------------------------
    public static bool Addr_IsUnspecified(object ip) { var a = A(ip); return a.ValEq((GoNetipAddr)IPv4Unspecified()) || a.ValEq((GoNetipAddr)IPv6Unspecified()); }
    public static bool Addr_IsLoopback(object ipo)
    {
        var ip = Unmapped(ipo);
        if (ip.Z == 4) return V4(ip, 0) == 127;
        if (ip.Z == 6) return ip.Hi == 0 && ip.Lo == 1;
        return false;
    }
    public static bool Addr_IsMulticast(object ipo)
    {
        var ip = Unmapped(ipo);
        if (ip.Z == 4) return (V4(ip, 0) & 0xf0) == 0xe0;
        if (ip.Z == 6) return (ip.Hi >> (64 - 8)) == 0xff;
        return false;
    }
    public static bool Addr_IsInterfaceLocalMulticast(object ipo)
    {
        var ip = A(ipo);
        if (Addr_Is6(ipo) && !Addr_Is4In6(ipo)) return (V6u16(ip, 0) & 0xff0f) == 0xff01;
        return false;
    }
    public static bool Addr_IsLinkLocalMulticast(object ipo)
    {
        var ip = Unmapped(ipo);
        if (ip.Z == 4) return V4(ip, 0) == 224 && V4(ip, 1) == 0 && V4(ip, 2) == 0;
        if (ip.Z == 6) return (V6u16(ip, 0) & 0xff0f) == 0xff02;
        return false;
    }
    public static bool Addr_IsLinkLocalUnicast(object ipo)
    {
        var ip = Unmapped(ipo);
        if (ip.Z == 4) return V4(ip, 0) == 169 && V4(ip, 1) == 254;
        if (ip.Z == 6) return (V6u16(ip, 0) & 0xffc0) == 0xfe80;
        return false;
    }
    public static bool Addr_IsGlobalUnicast(object ipo)
    {
        var a0 = A(ipo);
        if (a0.Z == 0) return false;
        var ip = Unmapped(ipo);
        if (ip.Z == 4 && (ip.ValEq((GoNetipAddr)IPv4Unspecified()) || ip.ValEq((GoNetipAddr)AddrFrom4(Fill(4, 255))))) return false;
        return !ip.ValEq((GoNetipAddr)IPv6Unspecified())
            && !Addr_IsLoopback(ip) && !Addr_IsMulticast(ip) && !Addr_IsLinkLocalUnicast(ip);
    }
    public static bool Addr_IsPrivate(object ipo)
    {
        var ip = Unmapped(ipo);
        if (ip.Z == 4) return V4(ip, 0) == 10 || (V4(ip, 0) == 172 && (V4(ip, 1) & 0xf0) == 16) || (V4(ip, 0) == 192 && V4(ip, 1) == 168);
        if (ip.Z == 6) return (V6(ip, 0) & 0xfe) == 0xfc;
        return false;
    }

    // ---- String (ported from appendTo4 / appendTo6) --------------------------------------
    public static GoString Addr_String(object ipo)
    {
        var ip = A(ipo);
        if (ip.Z == 0) return GoString.FromDotNetString("invalid IP");
        if (ip.Z == 4) return GoString.FromDotNetString(Str4(ip));
        if (Addr_Is4In6(ipo)) { var sb0 = new System.Text.StringBuilder("::ffff:"); sb0.Append(Str4((GoNetipAddr)Addr_Unmap(ipo))); if (ip.Zone.Length > 0) { sb0.Append('%'); sb0.Append(ip.Zone); } return GoString.FromDotNetString(sb0.ToString()); }
        return GoString.FromDotNetString(Str6(ip));
    }

    private static string Str4(GoNetipAddr ip) => $"{V4(ip, 0)}.{V4(ip, 1)}.{V4(ip, 2)}.{V4(ip, 3)}";

    private static string Str6(GoNetipAddr ip)
    {
        int zeroStart = 255, zeroEnd = 255;
        for (int i = 0; i < 8; i++)
        {
            int j = i;
            while (j < 8 && V6u16(ip, j) == 0) j++;
            int l = j - i;
            if (l >= 2 && l > zeroEnd - zeroStart) { zeroStart = i; zeroEnd = j; }
        }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            if (i == zeroStart) { sb.Append("::"); i = zeroEnd; if (i >= 8) break; }
            else if (i > 0) sb.Append(':');
            sb.Append(V6u16(ip, i).ToString("x"));
        }
        if (ip.Zone.Length > 0) { sb.Append('%'); sb.Append(ip.Zone); }
        return sb.ToString();
    }

    // ---- helpers -------------------------------------------------------------------------
    private static ulong U8(GoSlice b, int i) => (ulong)(byte)System.Convert.ToInt64(b.Data![b.Off + i]);
    private static int V4(GoNetipAddr ip, int i) => (int)((ip.Lo >> ((3 - i) * 8)) & 0xff);
    private static int V6(GoNetipAddr ip, int i) { ulong half = (i / 8) % 2 == 0 ? ip.Hi : ip.Lo; return (int)((half >> ((7 - i % 8) * 8)) & 0xff); }
    private static int V6u16(GoNetipAddr ip, int i) { ulong half = (i / 4) % 2 == 0 ? ip.Hi : ip.Lo; return (int)((half >> ((3 - i % 4) * 16)) & 0xffff); }
    private static GoNetipAddr Unmapped(object ipo) => Addr_Is4In6(ipo) ? (GoNetipAddr)Addr_Unmap(ipo) : A(ipo);
    private static void AddOne(GoNetipAddr a) { a.Lo = unchecked(a.Lo + 1); if (a.Lo == 0) a.Hi = unchecked(a.Hi + 1); }
    private static void SubOne(GoNetipAddr a) { if (a.Lo == 0) a.Hi = unchecked(a.Hi - 1); a.Lo = unchecked(a.Lo - 1); }
    private static GoSlice Zeros(int n) { var d = new object?[n]; for (int i = 0; i < n; i++) d[i] = 0; return new GoSlice { Data = d, Off = 0, Len = n, Cap = n }; }
    private static GoSlice Fill(int n, int v) { var s = Zeros(n); for (int i = 0; i < n; i++) s.Data![i] = v; return s; }
}
