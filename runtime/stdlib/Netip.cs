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

    // ---- parsing (ported from ParseAddr / parseIPv4 / parseIPv6) --------------------------
    private static object PErr(string @in, string msg, string at) =>
        new GoError(GoString.FromDotNetString(at.Length > 0
            ? "ParseAddr(" + Q(@in) + "): " + msg + " (at " + Q(at) + ")"
            : "ParseAddr(" + Q(@in) + "): " + msg));
    private static string Q(string s) => Strconv.Quote(GoString.FromDotNetString(s)).ToDotNetString();

    public static object?[] ParseAddr(GoString so)
    {
        string s = so.ToDotNetString();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '.') return ParseIPv4(s);
            if (s[i] == ':') return ParseIPv6(s);
            if (s[i] == '%') return new object?[] { new GoNetipAddr(), PErr(s, "missing IPv6 address", "") };
        }
        return new object?[] { new GoNetipAddr(), PErr(s, "unable to parse IP", "") };
    }
    public static object MustParseAddr(GoString s)
    {
        var r = ParseAddr(s);
        if (r[1] != null) throw new GoPanicException(((GoError)r[1]!).Error());
        return r[0]!;
    }

    private static object?[] ParseIPv4(string s)
    {
        var fields = new byte[4];
        var e = ParseIPv4Fields(s, 0, s.Length, fields, 0);
        if (e != null) return new object?[] { new GoNetipAddr(), e };
        return new object?[] { AddrFrom4(BytesOf(fields, 0, 4)), null };
    }

    private static object? ParseIPv4Fields(string @in, int off, int end, byte[] fields, int foff)
    {
        int val = 0, pos = 0, digLen = 0;
        string s = @in.Substring(off, end - off);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9')
            {
                if (digLen == 1 && val == 0) return PErr(@in, "IPv4 field has octet with leading zero", "");
                val = val * 10 + (c - '0'); digLen++;
                if (val > 255) return PErr(@in, "IPv4 field has value >255", "");
            }
            else if (c == '.')
            {
                if (i == 0 || i == s.Length - 1 || s[i - 1] == '.') return PErr(@in, "IPv4 field must have at least one digit", s.Substring(i));
                if (pos == 3) return PErr(@in, "IPv4 address too long", "");
                fields[foff + pos] = (byte)val; pos++; val = 0; digLen = 0;
            }
            else return PErr(@in, "unexpected character", s.Substring(i));
        }
        if (pos < 3) return PErr(@in, "IPv4 address too short", "");
        fields[foff + 3] = (byte)val;
        return null;
    }

    private static object?[] ParseIPv6(string @in)
    {
        string s = @in, zone = "";
        int i = s.IndexOf('%');
        if (i != -1) { zone = s.Substring(i + 1); s = s.Substring(0, i); if (zone.Length == 0) return new object?[] { new GoNetipAddr(), PErr(@in, "zone must be a non-empty string", "") }; }
        var ip = new byte[16];
        int ellipsis = -1;
        if (s.Length >= 2 && s[0] == ':' && s[1] == ':')
        {
            ellipsis = 0; s = s.Substring(2);
            if (s.Length == 0) return new object?[] { Addr_WithZone(IPv6Unspecified(), GoString.FromDotNetString(zone)), null };
        }
        i = 0;
        while (i < 16)
        {
            int off = 0; uint acc = 0;
            for (; off < s.Length; off++)
            {
                char c = s[off];
                if (c >= '0' && c <= '9') acc = (acc << 4) + (uint)(c - '0');
                else if (c >= 'a' && c <= 'f') acc = (acc << 4) + (uint)(c - 'a' + 10);
                else if (c >= 'A' && c <= 'F') acc = (acc << 4) + (uint)(c - 'A' + 10);
                else break;
                if (off > 3) return new object?[] { new GoNetipAddr(), PErr(@in, "each group must have 4 or less digits", s) };
                if (acc > 0xffff) return new object?[] { new GoNetipAddr(), PErr(@in, "IPv6 field has value >=2^16", s) };
            }
            if (off == 0) return new object?[] { new GoNetipAddr(), PErr(@in, "each colon-separated field must have at least one digit", s) };
            if (off < s.Length && s[off] == '.')
            {
                if (ellipsis < 0 && i != 12) return new object?[] { new GoNetipAddr(), PErr(@in, "embedded IPv4 address must replace the final 2 fields of the address", s) };
                if (i + 4 > 16) return new object?[] { new GoNetipAddr(), PErr(@in, "too many hex fields to fit an embedded IPv4 at the end of the address", s) };
                int end = @in.Length; if (zone.Length > 0) end -= zone.Length + 1;
                var e = ParseIPv4Fields(@in, end - s.Length, end, ip, i);
                if (e != null) return new object?[] { new GoNetipAddr(), e };
                s = ""; i += 4; break;
            }
            ip[i] = (byte)(acc >> 8); ip[i + 1] = (byte)acc; i += 2;
            s = s.Substring(off);
            if (s.Length == 0) break;
            if (s[0] != ':') return new object?[] { new GoNetipAddr(), PErr(@in, "unexpected character, want colon", s) };
            if (s.Length == 1) return new object?[] { new GoNetipAddr(), PErr(@in, "colon must be followed by more characters", s) };
            s = s.Substring(1);
            if (s[0] == ':')
            {
                if (ellipsis >= 0) return new object?[] { new GoNetipAddr(), PErr(@in, "multiple :: in address", s) };
                ellipsis = i; s = s.Substring(1);
                if (s.Length == 0) break;
            }
        }
        if (s.Length != 0) return new object?[] { new GoNetipAddr(), PErr(@in, "trailing garbage after address", s) };
        if (i < 16)
        {
            if (ellipsis < 0) return new object?[] { new GoNetipAddr(), PErr(@in, "address string too short", "") };
            int n = 16 - i;
            for (int j = i - 1; j >= ellipsis; j--) ip[j + n] = ip[j];
            for (int j = ellipsis; j < ellipsis + n; j++) ip[j] = 0;
        }
        else if (ellipsis >= 0) return new object?[] { new GoNetipAddr(), PErr(@in, "the :: must expand to at least one field of zeros", "") };
        return new object?[] { Addr_WithZone(AddrFrom16(BytesOf(ip, 0, 16)), GoString.FromDotNetString(zone)), null };
    }

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

    // ---- zone / expanded / marshal -------------------------------------------------------
    public static object Addr_WithZone(object ipo, GoString zoneS)
    {
        var ip = A(ipo);
        if (!Addr_Is6(ipo)) return ip;
        string zone = zoneS.ToDotNetString();
        var c = ip.Clone();
        c.Zone = zone; // z6noz when empty (Z stays 6)
        return c;
    }

    public static GoString Addr_StringExpanded(object ipo)
    {
        var ip = A(ipo);
        if (ip.Z == 0 || ip.Z == 4) return Addr_String(ipo);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 8; i++) { if (i > 0) sb.Append(':'); sb.Append(V6u16(ip, i).ToString("x4")); }
        if (ip.Zone.Length > 0) { sb.Append('%'); sb.Append(ip.Zone); }
        return GoString.FromDotNetString(sb.ToString());
    }

    public static GoSlice Addr_AppendTo(object ipo, GoSlice b) => Append(b, AppendBytes(ipo));
    public static object?[] Addr_AppendText(object ipo, GoSlice b) => new object?[] { Addr_AppendTo(ipo, b), null };
    public static object?[] Addr_MarshalText(object ipo) { var b = AppendBytes(ipo); return new object?[] { BytesOf(b, 0, b.Length), null }; }

    public static object?[] Addr_AppendBinary(object ipo, GoSlice b) => new object?[] { Append(b, BinBytes(ipo)), null };
    public static object?[] Addr_MarshalBinary(object ipo) { var b = BinBytes(ipo); return new object?[] { BytesOf(b, 0, b.Length), null }; }

    public static object? Addr_UnmarshalText(object ipo, GoSlice text)
    {
        var ip = A(ipo);
        if (text.Len == 0) { Reset(ip); return null; }
        var r = ParseAddr(GoString.FromDotNetString(BytesToStr(text)));
        if (r[1] != null) return r[1];
        Copy(ip, (GoNetipAddr)r[0]!); return null;
    }
    public static object? Addr_UnmarshalBinary(object ipo, GoSlice b)
    {
        var ip = A(ipo); int n = b.Len;
        if (n == 0) { Reset(ip); return null; }
        if (n == 4) { Copy(ip, (GoNetipAddr)AddrFrom4(b)); return null; }
        if (n == 16) { Copy(ip, (GoNetipAddr)AddrFrom16(b)); return null; }
        if (n > 16) { var a = (GoNetipAddr)Addr_WithZone(AddrFrom16(Sub(b, 0, 16)), GoString.FromDotNetString(BytesToStr(Sub(b, 16, n)))); Copy(ip, a); return null; }
        return new GoError(GoString.FromDotNetString("unexpected slice size"));
    }

    private static byte[] AppendBytes(object ipo)
    {
        var ip = A(ipo);
        if (ip.Z == 0) return System.Array.Empty<byte>();
        return System.Text.Encoding.ASCII.GetBytes(Addr_String(ipo).ToDotNetString());
    }
    private static byte[] BinBytes(object ipo)
    {
        var ip = A(ipo);
        if (ip.Z == 0) return System.Array.Empty<byte>();
        if (ip.Z == 4) { var r = new byte[4]; for (int i = 0; i < 4; i++) r[i] = (byte)((ip.Lo >> ((3 - i) * 8)) & 0xff); return r; }
        var zb = System.Text.Encoding.ASCII.GetBytes(ip.Zone);
        var b = new byte[16 + zb.Length];
        for (int i = 0; i < 8; i++) b[i] = (byte)((ip.Hi >> ((7 - i) * 8)) & 0xff);
        for (int i = 0; i < 8; i++) b[8 + i] = (byte)((ip.Lo >> ((7 - i) * 8)) & 0xff);
        zb.CopyTo(b, 16);
        return b;
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
    private static GoSlice BytesOf(byte[] src, int off, int n) { var d = new object?[n]; for (int i = 0; i < n; i++) d[i] = (int)src[off + i]; return new GoSlice { Data = d, Off = 0, Len = n, Cap = n }; }
    private static GoSlice Append(GoSlice b, byte[] add)
    {
        int n = b.Len; var d = new object?[n + add.Length];
        for (int i = 0; i < n; i++) d[i] = b.Data![b.Off + i];
        for (int i = 0; i < add.Length; i++) d[n + i] = (int)add[i];
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }
    private static GoSlice Sub(GoSlice b, int lo, int hi) => new() { Data = b.Data, Off = b.Off + lo, Len = hi - lo, Cap = hi - lo };
    private static string BytesToStr(GoSlice b) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < b.Len; i++) sb.Append((char)(byte)System.Convert.ToInt64(b.Data![b.Off + i])); return sb.ToString(); }
    private static void Reset(GoNetipAddr a) { a.Hi = 0; a.Lo = 0; a.Z = 0; a.Zone = ""; }
    private static void Copy(GoNetipAddr d, GoNetipAddr s) { d.Hi = s.Hi; d.Lo = s.Lo; d.Z = s.Z; d.Zone = s.Zone; }
}
