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
}
