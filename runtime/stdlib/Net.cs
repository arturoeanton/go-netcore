namespace GoCLR.Stdlib;

using System.Net;
using System.Net.Sockets;
using GoCLR.Runtime;

/// <summary>A net.Listener over a TcpListener.</summary>
public sealed class GoListener { public TcpListener L = null!; }

/// <summary>A net.Conn over a TcpClient/NetworkStream.</summary>
public sealed class GoConn { public TcpClient C = null!; public NetworkStream S = null!; }

/// <summary>Shim for a subset of Go's <c>net</c> (TCP client/server).</summary>
public static class Net
{
    private static (string host, int port) Parse(string addr)
    {
        int c = addr.LastIndexOf(':');
        string host = c <= 0 ? "" : addr.Substring(0, c);
        int port = int.Parse(addr.Substring(c + 1));
        return (host.Length == 0 ? "0.0.0.0" : host, port);
    }

    public static object?[] Listen(GoString network, GoString address)
    {
        try
        {
            var (host, port) = Parse(address.ToDotNetString());
            var ip = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host == "localhost" ? "127.0.0.1" : host);
            var l = new TcpListener(ip, port);
            l.Start();
            return new object?[] { new GoListener { L = l }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("listen: " + e.Message)) }; }
    }

    public static object?[] Dial(GoString network, GoString address)
    {
        try
        {
            var (host, port) = Parse(address.ToDotNetString());
            var client = new TcpClient(host == "" ? "127.0.0.1" : host, port);
            return new object?[] { new GoConn { C = client, S = client.GetStream() }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("dial: " + e.Message)) }; }
    }

    // net.Listener methods.
    public static object?[] Listener_Accept(object lo)
    {
        try { var c = ((GoListener)lo).L.AcceptTcpClient(); return new object?[] { new GoConn { C = c, S = c.GetStream() }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("accept: " + e.Message)) }; }
    }
    public static object? Listener_Close(object lo) { ((GoListener)lo).L.Stop(); return null; }

    // net.Conn methods.
    public static object?[] Conn_Read(object co, GoSlice p)
    {
        try
        {
            var buf = new byte[p.Len];
            int n = ((GoConn)co).S.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)buf[i];
            if (n == 0) return new object?[] { 0L, new GoError(GoString.FromDotNetString("EOF")) };
            return new object?[] { (long)n, null };
        }
        catch (System.Exception e) { return new object?[] { 0L, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object?[] Conn_Write(object co, GoSlice p)
    {
        try
        {
            var buf = new byte[p.Len];
            for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
            ((GoConn)co).S.Write(buf, 0, buf.Length);
            return new object?[] { (long)p.Len, null };
        }
        catch (System.Exception e) { return new object?[] { 0L, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object? Conn_Close(object co) { ((GoConn)co).C.Close(); return null; }

    // ---- UDP (PacketConn) --------------------------------------------------
    public static object?[] ListenPacket(GoString network, GoString address)
    {
        try
        {
            var (host, port) = Parse(address.ToDotNetString());
            var u = new UdpClient(new IPEndPoint(host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host == "localhost" ? "127.0.0.1" : host), port));
            return new object?[] { new GoPacketConn { U = u }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("listen udp: " + e.Message)) }; }
    }

    public static object?[] PC_WriteTo(object pc, GoSlice p, GoString addr)
    {
        try
        {
            var buf = new byte[p.Len];
            for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
            var (host, port) = Parse(addr.ToDotNetString());
            ((GoPacketConn)pc).U.Send(buf, buf.Length, host == "" ? "127.0.0.1" : host, port);
            return new object?[] { (long)p.Len, null };
        }
        catch (System.Exception e) { return new object?[] { 0L, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object?[] PC_ReadFrom(object pc, GoSlice p)
    {
        try
        {
            IPEndPoint? ep = null;
            byte[] data = ((GoPacketConn)pc).U.Receive(ref ep);
            int n = System.Math.Min(data.Length, p.Len);
            for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)data[i];
            return new object?[] { (long)n, GoString.FromDotNetString(ep?.ToString() ?? ""), null };
        }
        catch (System.Exception e) { return new object?[] { 0L, GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static object? PC_Close(object pc) { ((GoPacketConn)pc).U.Close(); return null; }

    // --- address parsing (net.IP / net.HardwareAddr are []byte) -------------
    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static GoSlice NilBytes() => new() { Data = null, Off = 0, Len = 0, Cap = 0 };

    // net.ParseIP(s) net.IP: the IP's bytes, or nil if s is not a valid IP.
    public static GoSlice ParseIP(GoString s) =>
        IPAddress.TryParse(s.ToDotNetString(), out var ip) ? Bytes(ip.GetAddressBytes()) : NilBytes();

    // net.ParseMAC(s) (net.HardwareAddr, error): parse a colon/hyphen-separated MAC.
    public static object?[] ParseMAC(GoString s)
    {
        var parts = s.ToDotNetString().Split(':', '-', '.');
        var bytes = new System.Collections.Generic.List<byte>();
        foreach (var p in parts)
        {
            for (int i = 0; i + 1 < p.Length + 1 && i < p.Length; i += 2)
            {
                if (i + 2 > p.Length || !byte.TryParse(p.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                    return new object?[] { NilBytes(), new GoError("address " + s.ToDotNetString() + ": invalid MAC address") };
                bytes.Add(b);
            }
        }
        if (bytes.Count is not (6 or 8 or 20))
            return new object?[] { NilBytes(), new GoError("address " + s.ToDotNetString() + ": invalid MAC address") };
        return new object?[] { Bytes(bytes.ToArray()), null };
    }

    // net.ParseCIDR(s) (net.IP, *net.IPNet, error).
    public static object?[] ParseCIDR(GoString s)
    {
        string str = s.ToDotNetString();
        int slash = str.IndexOf('/');
        if (slash < 0 || !IPAddress.TryParse(str.Substring(0, slash), out var ip) || !int.TryParse(str.Substring(slash + 1), out var bits))
            return new object?[] { NilBytes(), null, new GoError("invalid CIDR address: " + str) };
        var ipBytes = Bytes(ip.GetAddressBytes());
        return new object?[] { ipBytes, new GoNetAddr { Str = str, Port = bits, Ip = ipBytes }, null };
    }

    // net.IP methods (receiver is the []byte representation).
    private static byte[] Raw(GoSlice s)
    {
        if (s.Data == null) return System.Array.Empty<byte>();
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data[s.Off + i]);
        return b;
    }
    public static GoSlice IP_To4(GoSlice ip)
    {
        var b = Raw(ip);
        if (b.Length == 4) return ip;
        // IPv4-mapped IPv6 (::ffff:a.b.c.d)
        if (b.Length == 16)
        {
            bool mapped = true;
            for (int i = 0; i < 10; i++) if (b[i] != 0) { mapped = false; break; }
            if (mapped && b[10] == 0xff && b[11] == 0xff)
                return Bytes(new[] { b[12], b[13], b[14], b[15] });
        }
        return NilBytes();
    }
    public static GoSlice IP_To16(GoSlice ip) => Raw(ip).Length == 16 ? ip : ip;
    public static bool IP_Equal(GoSlice a, GoSlice b)
    {
        byte[] x = Raw(a), y = Raw(b);
        if (x.Length == y.Length) return x.AsSpan().SequenceEqual(y);
        var t4 = Raw(IP_To4(a)); var o4 = Raw(IP_To4(b));
        return t4.Length == 4 && o4.Length == 4 && t4.AsSpan().SequenceEqual(o4);
    }
    public static GoString IP_String(GoSlice ip)
    {
        var b = Raw(ip);
        try { return GoString.FromDotNetString(new IPAddress(b).ToString()); }
        catch { return GoString.FromDotNetString("<nil>"); }
    }

    // net.IPNet field reads (opaque GoNetAddr).
    public static GoSlice IPNet_IP(object n) => ((GoNetAddr)n).Ip ?? NilBytes();

    // net.SplitHostPort(hostport) (host, port string, err error).
    public static object?[] SplitHostPort(GoString hostport)
    {
        string s = hostport.ToDotNetString();
        int colon = s.LastIndexOf(':');
        if (colon < 0)
            return new object?[] { GoString.FromDotNetString(""), GoString.FromDotNetString(""), new GoError("address " + s + ": missing port in address") };
        string host = s.Substring(0, colon).TrimStart('[').TrimEnd(']');
        return new object?[] { GoString.FromDotNetString(host), GoString.FromDotNetString(s.Substring(colon + 1)), null };
    }

    // net sentinel errors.
    public static readonly GoError ErrClosedSentinel = new(GoString.FromDotNetString("use of closed network connection"));
    public static object ErrClosed() => ErrClosedSentinel;

    // net.JoinHostPort(host, port): "host:port", bracketing an IPv6 host.
    public static GoString JoinHostPort(GoString host, GoString port)
    {
        string h = host.ToDotNetString();
        if (h.Contains(':')) h = "[" + h + "]";
        return GoString.FromDotNetString(h + ":" + port.ToDotNetString());
    }

    // net.Resolve{TCP,UDP,IP,Unix}Addr: best-effort — parse host:port into an opaque
    // address. goclr does no DNS, so an unparseable host yields an error.
    private static object?[] ResolveAddr(GoString network, GoString address)
    {
        string a = address.ToDotNetString();
        return new object?[] { new GoNetAddr { Str = a }, null };
    }
    public static object?[] ResolveTCPAddr(GoString n, GoString a) => ResolveAddr(n, a);
    public static object?[] ResolveUDPAddr(GoString n, GoString a) => ResolveAddr(n, a);
    public static object?[] ResolveIPAddr(GoString n, GoString a) => ResolveAddr(n, a);
    public static object?[] ResolveUnixAddr(GoString n, GoString a) => ResolveAddr(n, a);
}

/// <summary>An opaque net address (net.IPNet / net.TCPAddr / net.UDPAddr / …).</summary>
public sealed class GoNetAddr { public string Str = ""; public long Port; public GoSlice? Ip; }

/// <summary>A net.PacketConn over a UdpClient.</summary>
public sealed class GoPacketConn { public UdpClient U = null!; }
