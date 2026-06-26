namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A net/mail.Address (parsed name + address).</summary>
public sealed class GoMailAddress { public string Name = ""; public string Address = ""; }

/// <summary>A net/mail.Message: a parsed header (map[string][]string) plus the body reader.</summary>
[GoShim("net/mail.Message")]
public sealed class GoMailMessage { public GoMap Header = GoMaps.Make(); public object? Body; }

/// <summary>Shim for a subset of Go's <c>net/mail</c> (address parsing).</summary>
public static class Mail
{
    // mail.ParseAddress(address) (*Address, error).
    public static object?[] ParseAddress(GoString s)
    {
        try
        {
            var m = new System.Net.Mail.MailAddress(s.ToDotNetString());
            return new object?[] { new GoMailAddress { Name = m.DisplayName, Address = m.Address }, null };
        }
        catch
        {
            return new object?[] { null, new GoError("mail: invalid address: " + s.ToDotNetString()) };
        }
    }

    // mail.ParseDate(date) (time.Time, error): parse an RFC 5322 date header. Go tries several
    // layouts (with/without the day-of-week and seconds, numeric or named zone); route each
    // through the time shim so the result is a real time.Time.
    private static readonly string[] DateLayouts =
    {
        "Mon, 02 Jan 2006 15:04:05 -0700", "Mon, 2 Jan 2006 15:04:05 -0700",
        "02 Jan 2006 15:04:05 -0700", "2 Jan 2006 15:04:05 -0700",
        "Mon, 02 Jan 2006 15:04:05 MST", "Mon, 02 Jan 2006 15:04 -0700",
        "Mon, 2 Jan 2006 15:04 -0700", "02 Jan 2006 15:04 -0700", "2 Jan 2006 15:04 -0700",
    };
    public static object?[] ParseDate(GoString date)
    {
        foreach (var layout in DateLayouts)
        {
            var r = Time.Parse(GoString.FromDotNetString(layout), date);
            if (r[1] == null) return r;
        }
        return new object?[] { new GoTime { IsZero = true }, new GoError("mail: header could not be parsed") };
    }

    // A zero-value Address + field setters so &mail.Address{Name:..., Address:...} literals work.
    public static object NewAddress() => new GoMailAddress();
    public static GoString Address_Name(object a) => GoString.FromDotNetString(((GoMailAddress)a).Name);
    public static GoString Address_Address(object a) => GoString.FromDotNetString(((GoMailAddress)a).Address);
    public static void Address_SetName(object a, GoString v) => ((GoMailAddress)a).Name = v.ToDotNetString();
    public static void Address_SetAddress(object a, GoString v) => ((GoMailAddress)a).Address = v.ToDotNetString();

    // mail.ErrHeaderNotPresent.
    public static readonly GoError ErrHeaderNotPresentSentinel = new(GoString.FromDotNetString("mail: header not in message"));
    public static object ErrHeaderNotPresent() => ErrHeaderNotPresentSentinel;

    // (*Address).String() — name always quoted for ASCII (escaping " and \); RFC 2047 "Q"
    // encoded for non-ASCII; the address itself is always angle-bracketed. Faithful to Go.
    public static GoString Address_String(object ao)
    {
        var a = (GoMailAddress)ao;
        string s = "<" + a.Address + ">";
        if (a.Name.Length == 0) return GoString.FromDotNetString(s);
        bool allPrintable = true;
        foreach (char c in a.Name)
            if (c >= 0x80 || (c < 0x20 && c != '\t')) { allPrintable = false; break; }
        var sb = new System.Text.StringBuilder();
        if (allPrintable)
        {
            sb.Append('"');
            foreach (char c in a.Name) { if (c == '"' || c == '\\') sb.Append('\\'); sb.Append(c); }
            sb.Append('"');
        }
        else
        {
            sb.Append("=?utf-8?q?");
            foreach (byte c in System.Text.Encoding.UTF8.GetBytes(a.Name))
            {
                if (c == ' ') sb.Append('_');
                else if (c >= 0x21 && c <= 0x7e && c != '=' && c != '?' && c != '_') sb.Append((char)c);
                else sb.Append('=').Append("0123456789ABCDEF"[c >> 4]).Append("0123456789ABCDEF"[c & 0xf]);
            }
            sb.Append("?=");
        }
        sb.Append(' ').Append(s);
        return GoString.FromDotNetString(sb.ToString());
    }

    // Split a list on top-level commas (commas inside double quotes stay together).
    private static System.Collections.Generic.List<string> SplitAddrs(string s)
    {
        var parts = new System.Collections.Generic.List<string>();
        var sb = new System.Text.StringBuilder();
        bool inq = false;
        foreach (char c in s)
        {
            if (c == '"') { inq = !inq; sb.Append(c); }
            else if (c == ',' && !inq) { parts.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts;
    }
    public static object?[] ParseAddressList(GoString s)
    {
        var list = new System.Collections.Generic.List<object?>();
        foreach (var part in SplitAddrs(s.ToDotNetString()))
        {
            var p = part.Trim();
            if (p.Length == 0) continue;
            var r = ParseAddress(GoString.FromDotNetString(p));
            if (r[1] != null) return new object?[] { default(GoSlice), r[1] };
            list.Add(r[0]);
        }
        if (list.Count == 0) return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("mail: no address")) };
        return new object?[] { new GoSlice { Data = list.ToArray(), Off = 0, Len = list.Count, Cap = list.Count }, null };
    }

    // (*AddressParser).Parse/ParseList — the parser's WordDecoder is unused under goclr.
    public static object?[] AddressParser_Parse(object p, GoString s) => ParseAddress(s);
    public static object?[] AddressParser_ParseList(object p, GoString s) => ParseAddressList(s);

    // mail.Header.Get(key) — canonical-key lookup (mail.Header is map[string][]string).
    public static GoString Header_Get(GoMap? h, GoString key) => Textproto.MIMEHeader_Get(h, key);

    // mail.Header.AddressList(key) ([]*Address, error): parse the header's address list, or
    // ErrHeaderNotPresent when the key is absent/empty (matching Go).
    public static object?[] Header_AddressList(GoMap? h, GoString key)
    {
        var v = Textproto.MIMEHeader_Get(h, key);
        if (v.Len == 0) return new object?[] { default(GoSlice), ErrHeaderNotPresentSentinel };
        return ParseAddressList(v);
    }

    // mail.ReadMessage(r) (*Message, error): parse the RFC 5322 header (via the textproto
    // MIME-header parser), leaving the remaining bytes as the body reader.
    public static object?[] ReadMessage(object? r)
    {
        var raw = Readers.Drain(r);
        var tp = new GoTextprotoReader { Data = raw };
        var hres = Textproto.Reader_ReadMIMEHeader(tp);
        if (hres[1] != null && !ReferenceEquals(hres[1], Io.EOFSentinel))
            return new object?[] { null, hres[1] };
        var body = new byte[raw.Length - tp.Pos];
        System.Array.Copy(raw, tp.Pos, body, 0, body.Length);
        return new object?[] { new GoMailMessage { Header = (GoMap)hres[0]!, Body = new GoReader { Data = body } }, null };
    }

    // *mail.Message field reads.
    public static GoMap Message_Header(object m) => ((GoMailMessage)m).Header;
    public static object? Message_Body(object m) => ((GoMailMessage)m).Body;
}
