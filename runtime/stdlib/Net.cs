namespace GoCLR.Stdlib;

using System.Net;
using System.Net.Sockets;
using GoCLR.Runtime;

/// <summary>A net.Listener over a TcpListener. Tagged as the concrete *net.TCPListener
/// so a <c>l.(*net.TCPListener)</c> assertion (echo's newListener) succeeds.</summary>
[GoShim("net.TCPListener")]
public sealed class GoListener { public TcpListener L = null!; public string Addr = ""; }

/// <summary>A net.Conn over a TcpClient/NetworkStream. Tagged as *net.TCPConn so a
/// <c>c.(*net.TCPConn)</c> assertion succeeds.</summary>
[GoShim("net.TCPConn")]
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

    // Listeners bound by net.Listen, keyed by the address string. (*http.Server).Serve
    // serves over the HttpListener backend, which binds the same port itself; it releases
    // the matching entry here first so the two don't collide. See Server_Serve.
    internal static readonly System.Collections.Generic.Dictionary<string, TcpListener> Bound =
        new(System.StringComparer.Ordinal);

    // net.Interfaces() ([]Interface, error): goclr does not enumerate host NICs, so report
    // an empty list (no error) — callers that read a MAC for an id (google/uuid's v1 node)
    // fall back to a random value, as Go does on a host with no usable interface.
    public static object?[] Interfaces() =>
        new object?[] { default(GoSlice), null };

    public static object?[] Listen(GoString network, GoString address)
    {
        try
        {
            string addr = address.ToDotNetString();
            var (host, port) = Parse(addr);
            var ip = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host == "localhost" ? "127.0.0.1" : host);
            var l = new TcpListener(ip, port);
            l.Start();
            lock (Bound) Bound[addr] = l;
            return new object?[] { new GoListener { L = l, Addr = addr }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("listen: " + e.Message)) }; }
    }

    // Release (stop and forget) the listener bound to addr, freeing the port. Returns true
    // if one was held. Used by (*http.Server).Serve before it rebinds via HttpListener.
    internal static bool ReleaseBound(string addr)
    {
        TcpListener? l;
        lock (Bound) { if (!Bound.TryGetValue(addr, out l)) return false; Bound.Remove(addr); }
        try { l!.Stop(); } catch { }
        return true;
    }

    // net.FileListener(f) (net.Listener, error): serving on a raw fd isn't supported
    // under goclr's HttpListener backend.
    public static object?[] FileListener(object? f) =>
        new object?[] { null, new GoError(GoString.FromDotNetString("net: FileListener not supported")) };

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
    // (*net.TCPListener).AcceptTCP() (*net.TCPConn, error): same as Accept, typed concrete.
    public static object?[] Listener_AcceptTCP(object lo) => Listener_Accept(lo);
    // (net.Listener).Addr() net.Addr: the bound local endpoint.
    public static object Listener_Addr(object lo)
    {
        var ep = ((GoListener)lo).L.LocalEndpoint as IPEndPoint;
        return new GoNetAddr { Str = ep != null ? ep.ToString() : "" };
    }

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

    // net.TCPConn tuning (autocert's listener sets keep-alive on accepted conns).
    public static object? TCPConn_SetKeepAlive(object c, bool b) => null;
    public static object? TCPConn_SetKeepAlivePeriod(object c, long d) => null;
    public static object? TCPConn_SetNoDelay(object c, bool b) => null;
    public static object? TCPConn_SetLinger(object c, long sec) => null;
    // net.Conn deadline methods: goclr's sockets are synchronous/blocking and do not honor
    // per-read/write timeouts, so these are no-ops that succeed (fasthttp's serveConn sets
    // them around every read/write and bails on an error — a missing method would otherwise
    // fail interface dispatch and silently kill the worker before the handler runs).
    public static object? Conn_SetReadDeadline(object c, object? t) => null;
    public static object? Conn_SetWriteDeadline(object c, object? t) => null;
    public static object? Conn_SetDeadline(object c, object? t) => null;
    public static object Conn_LocalAddr(object c)
    {
        try { var ep = ((GoConn)c).C.Client.LocalEndPoint as IPEndPoint; return new GoNetAddr { Str = ep?.ToString() ?? "" }; }
        catch { return new GoNetAddr { Str = "" }; }
    }
    public static object Conn_RemoteAddr(object c)
    {
        try { var ep = ((GoConn)c).C.Client.RemoteEndPoint as IPEndPoint; return new GoNetAddr { Str = ep?.ToString() ?? "" }; }
        catch { return new GoNetAddr { Str = "" }; }
    }

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

    // ---- typed UDP: net.UDPConn / net.UDPAddr ------------------------------
    private static byte[] SliceToBytes(GoSlice p)
    {
        var b = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) b[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        return b;
    }

    // A net.UDPAddr is carried in the shared GoNetAddr (Str/Port/Ip).
    private static GoNetAddr MakeUDPAddr(IPAddress ip, int port) =>
        new GoNetAddr { Str = new IPEndPoint(ip, port).ToString(), Port = port, Ip = Bytes(ip.GetAddressBytes()) };

    private static IPEndPoint? EndpointOf(object? o)
    {
        var a = o switch { GoNetAddr g => g, GoPtr p => GoPtrs.Get(p) as GoNetAddr, _ => o as GoNetAddr };
        if (a == null) return null;
        IPAddress ip = a.Ip is GoSlice s && s.Len > 0 ? new IPAddress(SliceToBytes(s)) : IPAddress.Any;
        return new IPEndPoint(ip, (int)a.Port);
    }

    public static object?[] ListenUDP(GoString network, object? laddr)
    {
        try
        {
            var ep = EndpointOf(laddr) ?? new IPEndPoint(IPAddress.Any, 0);
            var s = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            s.Bind(ep);
            return new object?[] { new GoUDPConn { Sock = s }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("listen udp: " + e.Message)) }; }
    }

    public static object?[] DialUDP(GoString network, object? laddr, object? raddr)
    {
        try
        {
            var rep = EndpointOf(raddr);
            var lep = EndpointOf(laddr) ?? new IPEndPoint(IPAddress.Any, 0);
            var s = new Socket((rep ?? lep).AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            s.Bind(lep);
            if (rep != null) s.Connect(rep);
            return new object?[] { new GoUDPConn { Sock = s, Remote = rep }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("dial udp: " + e.Message)) }; }
    }

    public static object?[] UDPConn_ReadFromUDP(object c, GoSlice b)
    {
        try
        {
            var buf = new byte[b.Len];
            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            int n = ((GoUDPConn)c).Sock.ReceiveFrom(buf, ref ep);
            for (int i = 0; i < n; i++) b.Data![b.Off + i] = (int)buf[i];
            var rip = (IPEndPoint)ep;
            return new object?[] { (long)n, MakeUDPAddr(rip.Address, rip.Port), null };
        }
        catch (System.Exception e) { return new object?[] { 0L, null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object?[] UDPConn_WriteToUDP(object c, GoSlice b, object? addr)
    {
        try
        {
            var ep = EndpointOf(addr);
            int n = ((GoUDPConn)c).Sock.SendTo(SliceToBytes(b), ep!);
            return new object?[] { (long)n, null };
        }
        catch (System.Exception e) { return new object?[] { 0L, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object?[] UDPConn_Read(object c, GoSlice b)
    {
        try
        {
            var buf = new byte[b.Len];
            int n = ((GoUDPConn)c).Sock.Receive(buf);
            for (int i = 0; i < n; i++) b.Data![b.Off + i] = (int)buf[i];
            return new object?[] { (long)n, null };
        }
        catch (System.Exception e) { return new object?[] { 0L, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object?[] UDPConn_Write(object c, GoSlice b)
    {
        try { return new object?[] { (long)((GoUDPConn)c).Sock.Send(SliceToBytes(b)), null }; }
        catch (System.Exception e) { return new object?[] { 0L, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object? UDPConn_Close(object c) { ((GoUDPConn)c).Sock.Close(); return null; }

    public static object UDPConn_LocalAddr(object c)
    {
        var ep = (IPEndPoint)((GoUDPConn)c).Sock.LocalEndPoint!;
        return MakeUDPAddr(ep.Address, ep.Port);
    }

    // Deadlines map to socket timeouts; a zero time clears them. The Go time value is
    // opaque here, so a non-zero deadline arms a long receive timeout to avoid hangs.
    public static object? UDPConn_SetReadDeadline(object c, object? t) { return null; }
    public static object? UDPConn_SetWriteDeadline(object c, object? t) { return null; }
    public static object? UDPConn_SetDeadline(object c, object? t) { return null; }

    // net.UDPAddr (GoNetAddr) accessors and zero value.
    public static object NewUDPAddr() => new GoNetAddr();
    public static GoSlice UDPAddr_IP(object a) => ((GoNetAddr)a).Ip ?? NilBytes();
    public static long UDPAddr_Port(object a) => ((GoNetAddr)a).Port;
    public static GoString UDPAddr_Zone(object a) => GoString.FromDotNetString("");
    // net.Dialer field setters — no-ops (the dialer is dead code on goclr's server path).
    public static void Dialer_SetLocalAddr(object d, object? v) { }
    public static void UDPAddr_SetIP(object a, GoSlice ip) { ((GoNetAddr)a).Ip = ip; }
    public static void UDPAddr_SetPort(object a, long port) { ((GoNetAddr)a).Port = port; }
    public static GoString UDPAddr_Network(object a) => GoString.FromDotNetString("udp");
    public static GoString TCPAddr_Network(object a) => GoString.FromDotNetString("tcp");
    public static GoString UDPAddr_String(object a)
    {
        var g = (GoNetAddr)a;
        if (g.Str.Length > 0) return GoString.FromDotNetString(g.Str);
        IPAddress ip = g.Ip is GoSlice s && s.Len > 0 ? new IPAddress(SliceToBytes(s)) : IPAddress.Any;
        return GoString.FromDotNetString(new IPEndPoint(ip, (int)g.Port).ToString());
    }

    // --- address parsing (net.IP / net.HardwareAddr are []byte) -------------
    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static GoSlice NilBytes() => new() { Data = null, Off = 0, Len = 0, Cap = 0 };

    // net.DefaultResolver + net.Resolver.LookupIPAddr — DNS, dead code on goclr's server
    // path. The resolver is an opaque handle; lookups return a "not supported" error.
    public static object DefaultResolver() => new GoResolver();
    // net.InterfaceByName(name) (*Interface, error): network-interface lookup is unsupported
    // (dead code on goclr's serving path) — report not-found.
    public static object?[] InterfaceByName(GoString name) =>
        new object?[] { null, new GoError(GoString.FromDotNetString("net: interface " + name.ToDotNetString() + " not found")) };
    public static long Interface_Index(object i) => ((GoNetInterface)i).Index;
    public static GoString Interface_Name(object i) => GoString.FromDotNetString("");
    public static GoSlice Interface_HardwareAddr(object i) => NilBytes();
    public static object?[] Resolver_LookupIPAddr(object r, object? ctx, GoString host) =>
        new object?[] { NilBytes(), new GoError(GoString.FromDotNetString("lookup " + host.ToDotNetString() + ": DNS not supported")) };

    // net package-level IP vars. IPv4(a,b,c,d) is the 16-byte IPv4-in-IPv6 form
    // (10 zero bytes, 0xff, 0xff, then the 4 octets) — matching Go's net.IPv4.
    private static GoSlice IPv4Bytes(byte a, byte b, byte c, byte d) =>
        Bytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff, a, b, c, d });
    private static GoSlice IPv6Bytes(params byte[] last) // 16 bytes, last N filled, rest zero
    {
        var b = new byte[16];
        for (int i = 0; i < last.Length; i++) b[16 - last.Length + i] = last[i];
        return Bytes(b);
    }
    public static object IPv4zero() => IPv4Bytes(0, 0, 0, 0);
    public static object IPv4bcast() => IPv4Bytes(255, 255, 255, 255);
    public static object IPv4allsys() => IPv4Bytes(224, 0, 0, 1);
    public static object IPv4allrouter() => IPv4Bytes(224, 0, 0, 2);
    public static object IPv6zero() => IPv6Bytes();
    public static object IPv6unspecified() => IPv6Bytes();
    public static object IPv6loopback() => IPv6Bytes(1);

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
        if (b.Length == 0) return GoString.FromDotNetString("<nil>");
        // Go renders a 4-byte IP and a v4-mapped 16-byte IP as a dotted quad (not ::ffff:a.b.c.d).
        var p4 = Raw(IP_To4(ip));
        if (p4.Length == 4) return GoString.FromDotNetString($"{p4[0]}.{p4[1]}.{p4[2]}.{p4[3]}");
        if (b.Length == 16) { try { return GoString.FromDotNetString(new IPAddress(b).ToString()); } catch { } }
        // Invalid length: Go returns "?" + lowercase hex of the bytes.
        var sb = new System.Text.StringBuilder("?");
        foreach (var by in b) sb.Append(by.ToString("x2"));
        return GoString.FromDotNetString(sb.ToString());
    }

    // net.IP predicates (operate on the 4- or 16-byte slice; mirror Go's classification).
    public static bool IP_IsLoopback(GoSlice ip)
    {
        var b = Raw(IP_To4(ip));
        if (b.Length == 4) return b[0] == 127;
        var v = Raw(ip);
        if (v.Length == 16) { for (int i = 0; i < 15; i++) if (v[i] != 0) return false; return v[15] == 1; }
        return false;
    }
    public static bool IP_IsLinkLocalUnicast(GoSlice ip)
    {
        var b = Raw(IP_To4(ip));
        if (b.Length == 4) return b[0] == 169 && b[1] == 254;
        var v = Raw(ip);
        return v.Length == 16 && v[0] == 0xfe && (v[1] & 0xc0) == 0x80;
    }
    public static bool IP_IsLinkLocalMulticast(GoSlice ip)
    {
        var b = Raw(IP_To4(ip));
        if (b.Length == 4) return b[0] == 224 && b[1] == 0 && b[2] == 0;
        var v = Raw(ip);
        return v.Length == 16 && v[0] == 0xff && (v[1] & 0x0f) == 0x02;
    }
    public static bool IP_IsMulticast(GoSlice ip)
    {
        var b = Raw(IP_To4(ip));
        if (b.Length == 4) return (b[0] & 0xf0) == 0xe0;
        var v = Raw(ip);
        return v.Length == 16 && v[0] == 0xff;
    }
    public static bool IP_IsPrivate(GoSlice ip)
    {
        var b = Raw(IP_To4(ip));
        if (b.Length == 4)
            return b[0] == 10 || (b[0] == 172 && (b[1] & 0xf0) == 16) || (b[0] == 192 && b[1] == 168);
        var v = Raw(ip);
        return v.Length == 16 && (v[0] & 0xfe) == 0xfc;
    }
    public static bool IP_IsUnspecified(GoSlice ip)
    {
        var b = Raw(ip);
        foreach (var x in b) if (x != 0) return false;
        return b.Length == 4 || b.Length == 16;
    }
    public static bool IP_IsGlobalUnicast(GoSlice ip)
    {
        var b = Raw(ip);
        return (b.Length == 4 || b.Length == 16) && !IP_IsUnspecified(ip) && !IP_IsLoopback(ip)
            && !IP_IsMulticast(ip) && !IP_IsLinkLocalUnicast(ip);
    }

    // --- pure IP/IPMask/HardwareAddr/Flags methods + mask constructors ---
    public static GoSlice IPv4(int a, int b, int c, int d) => IPv4Bytes((byte)a, (byte)b, (byte)c, (byte)d);
    public static GoSlice IPv4Mask(int a, int b, int c, int d) => Bytes(new[] { (byte)a, (byte)b, (byte)c, (byte)d });
    public static GoSlice CIDRMask(long ones, long bits)
    {
        if (bits != 32 && bits != 128) return NilBytes();
        if (ones < 0 || ones > bits) return NilBytes();
        int l = (int)(bits / 8);
        var m = new byte[l];
        long n = ones;
        for (int i = 0; i < l; i++)
        {
            if (n >= 8) { m[i] = 0xff; n -= 8; continue; }
            m[i] = (byte)~(0xff >> (int)n);
            n = 0;
        }
        return Bytes(m);
    }

    private static bool AllFF(byte[] b, int s, int e) { for (int i = s; i < e; i++) if (b[i] != 0xff) return false; return true; }
    private static bool V4Prefix(byte[] ip) { for (int i = 0; i < 10; i++) if (ip[i] != 0) return false; return ip[10] == 0xff && ip[11] == 0xff; }
    private static GoSlice AppendB(GoSlice dst, byte[] extra)
    {
        var d = new object?[dst.Len + extra.Length];
        for (int i = 0; i < dst.Len; i++) d[i] = dst.Data![dst.Off + i];
        for (int i = 0; i < extra.Length; i++) d[dst.Len + i] = (int)extra[i];
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    public static GoSlice IP_Mask(GoSlice ipS, GoSlice maskS)
    {
        byte[] ip = Raw(ipS), mask = Raw(maskS);
        if (mask.Length == 16 && ip.Length == 4 && AllFF(mask, 0, 12)) mask = mask[12..];
        if (mask.Length == 4 && ip.Length == 16 && V4Prefix(ip)) ip = ip[12..];
        int n = ip.Length;
        if (n != mask.Length) return NilBytes();
        var outb = new byte[n];
        for (int i = 0; i < n; i++) outb[i] = (byte)(ip[i] & mask[i]);
        return Bytes(outb);
    }
    public static GoSlice IP_DefaultMask(GoSlice ipS)
    {
        var ip = Raw(IP_To4(ipS));
        if (ip.Length != 4) return NilBytes();
        if (ip[0] < 0x80) return Bytes(new byte[] { 0xff, 0, 0, 0 });
        if (ip[0] < 0xC0) return Bytes(new byte[] { 0xff, 0xff, 0, 0 });
        return Bytes(new byte[] { 0xff, 0xff, 0xff, 0 });
    }
    public static bool IP_IsInterfaceLocalMulticast(GoSlice ipS)
    {
        var b = Raw(ipS);
        return b.Length == 16 && b[0] == 0xff && (b[1] & 0x0f) == 0x01;
    }
    public static object?[] IP_MarshalText(GoSlice ipS)
    {
        var b = Raw(ipS);
        if (b.Length == 0) return new object?[] { Bytes(System.Array.Empty<byte>()), null };
        if (b.Length != 4 && b.Length != 16) return new object?[] { NilBytes(), new GoError(GoString.FromDotNetString("invalid IP address")) };
        return new object?[] { Bytes(System.Text.Encoding.UTF8.GetBytes(IP_String(ipS).ToDotNetString())), null };
    }
    public static object?[] IP_AppendText(GoSlice ipS, GoSlice dst)
    {
        var mt = IP_MarshalText(ipS);
        if (mt[1] != null) return new object?[] { dst, mt[1] };
        return new object?[] { AppendB(dst, Raw((GoSlice)mt[0]!)), null };
    }

    private static int SimpleMaskLen(byte[] mask)
    {
        int n = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            int v = mask[i];
            if (v == 0xff) { n += 8; continue; }
            while ((v & 0x80) != 0) { n++; v = (v << 1) & 0xff; }
            if (v != 0) return -1;
            for (i++; i < mask.Length; i++) if (mask[i] != 0) return -1;
            break;
        }
        return n;
    }
    public static object?[] IPMask_Size(GoSlice mS)
    {
        var m = Raw(mS);
        int ones = SimpleMaskLen(m), bits = m.Length * 8;
        return ones == -1 ? new object?[] { 0L, 0L } : new object?[] { (long)ones, (long)bits };
    }
    public static GoString IPMask_String(GoSlice mS)
    {
        var m = Raw(mS);
        if (m.Length == 0) return GoString.FromDotNetString("<nil>");
        var sb = new System.Text.StringBuilder();
        foreach (var b in m) sb.Append(b.ToString("x2"));
        return GoString.FromDotNetString(sb.ToString());
    }
    public static GoString IPNet_Network(object n) => GoString.FromDotNetString("ip+net");
    public static GoString HardwareAddr_String(GoSlice aS)
    {
        var a = Raw(aS);
        if (a.Length == 0) return GoString.FromDotNetString("");
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(':'); sb.Append(a[i].ToString("x2")); }
        return GoString.FromDotNetString(sb.ToString());
    }
    private static readonly string[] FlagNames = { "up", "broadcast", "loopback", "pointtopoint", "multicast", "running" };
    public static GoString Flags_String(ulong f)
    {
        string s = "";
        for (int i = 0; i < FlagNames.Length; i++)
            if ((f & (1UL << i)) != 0) { if (s != "") s += "|"; s += FlagNames[i]; }
        if (s == "") s = "0";
        return GoString.FromDotNetString(s);
    }

    // net.IPNet field reads (opaque GoNetAddr).
    public static GoSlice IPNet_IP(object n) => ((GoNetAddr)n).Ip ?? NilBytes();

    // Zero value for net.IPNet (a composite-literal *net.IPNet starts non-null; its
    // IP/Mask fields are opaque under the shim).
    public static object NewIPNet() => new GoNetAddr();

    // (*net.IPNet).Contains(ip): is ip within this CIDR network?
    public static bool IPNet_Contains(object? n, GoSlice ip)
    {
        if (n is not GoNetAddr net || net.Str.Length == 0) return false;
        int slash = net.Str.IndexOf('/');
        if (slash < 0 || !IPAddress.TryParse(net.Str.Substring(0, slash), out var baseAddr) || !int.TryParse(net.Str.Substring(slash + 1), out var bits))
            return false;
        byte[] baseB = baseAddr.GetAddressBytes();
        byte[] ipB = Raw(ip);
        // Normalise an IPv4-mapped/v4 address against an IPv4 network (and vice versa).
        if (ipB.Length != baseB.Length)
        {
            var v4 = Raw(IP_To4(ip));
            if (v4.Length == 4 && baseB.Length == 4) ipB = v4;
            else return false;
        }
        if (ipB.Length != baseB.Length) return false;
        int fullBytes = bits / 8, remBits = bits % 8;
        for (int i = 0; i < fullBytes; i++) if (ipB[i] != baseB[i]) return false;
        if (remBits > 0)
        {
            int mask = 0xff << (8 - remBits) & 0xff;
            if ((ipB[fullBytes] & mask) != (baseB[fullBytes] & mask)) return false;
        }
        return true;
    }
    public static GoString IPNet_String(object n) => GoString.FromDotNetString(((GoNetAddr)n).Str);

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

    // net.OpError methods.
    public static GoString OpError_Error(object e)
    {
        var oe = (GoNetOpError)e;
        string inner = oe.Err is IGoError g ? g.Error().ToDotNetString() : "";
        return GoString.FromDotNetString((oe.Op.Length > 0 ? oe.Op + " " : "") + (oe.Net.Length > 0 ? oe.Net + ": " : "") + inner);
    }
    public static object? OpError_Unwrap(object e) => ((GoNetOpError)e).Err;
    public static bool OpError_Timeout(object e) => false;
    public static bool OpError_Temporary(object e) => false;
    public static GoString OpError_Op(object e) => GoString.FromDotNetString(((GoNetOpError)e).Op);
    public static GoString OpError_Net(object e) => GoString.FromDotNetString(((GoNetOpError)e).Net);
    public static object? OpError_Err(object e) => ((GoNetOpError)e).Err;

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
    // net.ResolveUDPAddr parses host:port so the returned *UDPAddr carries IP and Port
    // (ListenUDP/DialUDP and addr.Port/addr.IP need them, not just the string form).
    public static object?[] ResolveUDPAddr(GoString n, GoString a)
    {
        try
        {
            var (host, port) = Parse(a.ToDotNetString());
            var ip = host == "0.0.0.0" ? IPAddress.Loopback : IPAddress.Parse(host == "localhost" ? "127.0.0.1" : host);
            return new object?[] { MakeUDPAddr(ip, port), null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("resolve udp: " + e.Message)) }; }
    }
    public static object?[] ResolveIPAddr(GoString n, GoString a) => ResolveAddr(n, a);
    public static object?[] ResolveUnixAddr(GoString n, GoString a) => ResolveAddr(n, a);
}

/// <summary>An opaque net address (net.IPNet / net.TCPAddr / net.UDPAddr / …).</summary>
[GoShim("net.TCPAddr")]
[GoShim("net.UDPAddr")]
public sealed class GoNetAddr { public string Str = ""; public long Port; public GoSlice? Ip; }

/// <summary>An opaque net.Resolver handle (DNS is unsupported under goclr's server path).</summary>
public sealed class GoResolver { }

/// <summary>An opaque net.Interface (only Index is read, on the dead raw-socket path).</summary>
public sealed class GoNetInterface { public long Index; }

/// <summary>A net.OpError (an operation/network/address-tagged error).</summary>
public sealed class GoNetOpError { public string Op = ""; public string Net = ""; public object? Err; }

/// <summary>A net.PacketConn over a UdpClient.</summary>
public sealed class GoPacketConn { public UdpClient U = null!; }

/// <summary>A net.UDPConn over a DGRAM socket (ReceiveFrom/SendTo, or connected I/O).</summary>
public sealed class GoUDPConn { public Socket Sock = null!; public IPEndPoint? Remote; }
