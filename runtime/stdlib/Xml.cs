namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GoCLR.Runtime;

/// <summary>An xml.Name (namespace + local name).</summary>
public sealed class GoXmlName { public string Space = ""; public string Local = ""; }

/// <summary>An xml.Attr (a name/value attribute).</summary>
public sealed class GoXmlAttr { public GoXmlName Name = new(); public string Value = ""; }

/// <summary>An xml.StartElement token.</summary>
public sealed class GoXmlStart { public GoXmlName Name = new(); public GoSlice Attr; }

/// <summary>An xml.EndElement token.</summary>
public sealed class GoXmlEnd { public GoXmlName Name = new(); }

/// <summary>An xml.Encoder writing to an underlying writer.</summary>
public sealed class GoXmlEncoder
{
    public object? W;
    public readonly StringBuilder Buf = new();
    public string IndentPrefix = "";
    public string IndentStr = "";
    public int Depth;
}

/// <summary>Shim for a subset of Go's <c>encoding/xml</c>: reflection-based Marshal/Encode
/// plus the token API (StartElement/EndElement/CharData). Go's encoder rejects maps; this
/// shim instead emits <c>&lt;map&gt;…&lt;/map&gt;</c>, so gin.H renders without a custom
/// marshaler.</summary>
public static class Xml
{
    public const string HeaderConst = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
    public static object Header() => GoString.FromDotNetString(HeaderConst);

    // Zero-value constructors for the value-type tokens.
    public static object NewXmlName() => new GoXmlName();
    public static object NewXmlStart() => new GoXmlStart();
    public static object NewXmlEnd() => new GoXmlEnd();
    public static object NewXmlAttr() => new GoXmlAttr();

    // xml.Name field get/set.
    public static GoString Name_Space(object n) => GoString.FromDotNetString(((GoXmlName)n).Space);
    public static GoString Name_Local(object n) => GoString.FromDotNetString(((GoXmlName)n).Local);
    public static void Name_SetSpace(object n, GoString v) => ((GoXmlName)n).Space = v.ToDotNetString();
    public static void Name_SetLocal(object n, GoString v) => ((GoXmlName)n).Local = v.ToDotNetString();

    // xml.StartElement / EndElement / Attr field get/set.
    public static object Start_Name(object s) => ((GoXmlStart)s).Name;
    public static GoSlice Start_Attr(object s) => ((GoXmlStart)s).Attr;
    public static void Start_SetName(object s, object? v) => ((GoXmlStart)s).Name = v as GoXmlName ?? new GoXmlName();
    public static void Start_SetAttr(object s, GoSlice v) => ((GoXmlStart)s).Attr = v;
    public static object End_Name(object e) => ((GoXmlEnd)e).Name;
    public static void End_SetName(object e, object? v) => ((GoXmlEnd)e).Name = v as GoXmlName ?? new GoXmlName();
    public static object Attr_Name(object a) => ((GoXmlAttr)a).Name;
    public static GoString Attr_Value(object a) => GoString.FromDotNetString(((GoXmlAttr)a).Value);
    public static void Attr_SetName(object a, object? v) => ((GoXmlAttr)a).Name = v as GoXmlName ?? new GoXmlName();
    public static void Attr_SetValue(object a, GoString v) => ((GoXmlAttr)a).Value = v.ToDotNetString();

    // xml.Marshal(v) ([]byte, error).
    public static object?[] Marshal(object? v)
    {
        var sb = new StringBuilder();
        try { WriteValue(sb, v, null); }
        catch (System.Exception e) { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("xml: " + e.Message)) }; }
        return new object?[] { Bytes(sb.ToString()), null };
    }

    public static object?[] MarshalIndent(object? v, GoString prefix, GoString indent)
    {
        var enc = new GoXmlEncoder { IndentPrefix = prefix.ToDotNetString(), IndentStr = indent.ToDotNetString() };
        try { WriteValueIndented(enc, v, null); }
        catch (System.Exception e) { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("xml: " + e.Message)) }; }
        return new object?[] { Bytes(enc.Buf.ToString()), null };
    }

    // xml.NewEncoder(w) *Encoder.
    public static object NewEncoder(object? w) => new GoXmlEncoder { W = w };

    public static object? Encoder_Encode(object e, object? v)
    {
        var enc = (GoXmlEncoder)e;
        if (enc.IndentStr.Length > 0 || enc.IndentPrefix.Length > 0) WriteValueIndented(enc, v, null);
        else WriteValue(enc.Buf, v, null);
        return Encoder_Flush(e);
    }
    public static object? Encoder_EncodeElement(object e, object? v, object start)
    {
        var enc = (GoXmlEncoder)e;
        var st = (GoXmlStart)start;
        WriteValue(enc.Buf, v, ElemName(st));
        return Encoder_Flush(e);
    }
    // EncodeToken(t): write a single start/end/char token.
    public static object? Encoder_EncodeToken(object e, object? t)
    {
        var enc = (GoXmlEncoder)e;
        switch (t)
        {
            case GoXmlStart st:
                enc.Buf.Append('<').Append(ElemName(st));
                if (st.Attr.Data != null)
                    for (int i = 0; i < st.Attr.Len; i++)
                        if (st.Attr.Data[st.Attr.Off + i] is GoXmlAttr a)
                            enc.Buf.Append(' ').Append(AttrName(a.Name)).Append("=\"").Append(Escape(a.Value)).Append('"');
                enc.Buf.Append('>');
                break;
            case GoXmlEnd en:
                enc.Buf.Append("</").Append(NameStr(en.Name)).Append('>');
                break;
            case GoString cd: // xml.CharData
                enc.Buf.Append(Escape(cd.ToDotNetString()));
                break;
        }
        return null;
    }
    public static object? Encoder_Flush(object e)
    {
        var enc = (GoXmlEncoder)e;
        if (enc.Buf.Length > 0 && enc.W != null)
        {
            Compress.WriteRaw(enc.W, Encoding.UTF8.GetBytes(enc.Buf.ToString()));
            enc.Buf.Clear();
        }
        return null;
    }
    public static void Encoder_Indent(object e, GoString prefix, GoString indent)
    {
        var enc = (GoXmlEncoder)e;
        enc.IndentPrefix = prefix.ToDotNetString();
        enc.IndentStr = indent.ToDotNetString();
    }
    public static object? Encoder_Close(object e) => Encoder_Flush(e);

    // xml.Unmarshal / Decoder: goclr does not parse XML into structs.
    public static object? Unmarshal(GoSlice data, object? v) => new GoError(GoString.FromDotNetString("xml: decoding is not supported under goclr"));
    public static object NewDecoder(object? r) => new GoXmlDecoder { R = r };
    public static object? Decoder_Decode(object d, object? v) => new GoError(GoString.FromDotNetString("xml: decoding is not supported under goclr"));
    public static object?[] Decoder_Token(object d) => new object?[] { null, Io.EOFSentinel };

    // ---- escaping, token copies, and constants ----
    private static byte[] RawBytes(GoSlice b) { var x = new byte[b.Len]; for (int i = 0; i < b.Len; i++) x[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]); return x; }
    private static string XmlEscape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch
            {
                '"' => "&#34;", '\'' => "&#39;", '&' => "&amp;", '<' => "&lt;", '>' => "&gt;",
                '\t' => "&#x9;", '\n' => "&#xA;", '\r' => "&#xD;", _ => c.ToString(),
            });
        return sb.ToString();
    }
    public static object? EscapeText(object? w, GoSlice s) { Fmt.WriteTo(w, XmlEscape(GoString.FromBytes(RawBytes(s)).ToDotNetString())); return null; }
    public static void Escape(object? w, GoSlice s) { Fmt.WriteTo(w, XmlEscape(GoString.FromBytes(RawBytes(s)).ToDotNetString())); }

    private static GoSlice CopyBytes(GoSlice b) { var d = new object?[b.Len]; for (int i = 0; i < b.Len; i++) d[i] = b.Data![b.Off + i]; return new GoSlice { Data = d, Off = 0, Len = b.Len, Cap = b.Len }; }
    public static GoSlice CharData_Copy(GoSlice b) => CopyBytes(b); // CharData = []byte
    public static GoSlice Comment_Copy(GoSlice b) => CopyBytes(b);  // Comment = []byte
    public static GoSlice Directive_Copy(GoSlice b) => CopyBytes(b); // Directive = []byte
    public static object ProcInst_Copy(object o) => o;               // a struct value: the receiver copy suffices
    public static object StartElement_Copy(object s)
    {
        var st = (GoXmlStart)s;
        return new GoXmlStart { Name = new GoXmlName { Space = st.Name.Space, Local = st.Name.Local }, Attr = st.Attr.Data == null ? st.Attr : CopyBytes(st.Attr) };
    }
    public static object StartElement_End(object s) { var st = (GoXmlStart)s; return new GoXmlEnd { Name = new GoXmlName { Space = st.Name.Space, Local = st.Name.Local } }; }

    public static GoString TagPathError_Error(object e) => GoString.FromDotNetString("xml: bad tag path");
    public static GoString UnmarshalError_Error(GoString s) => s; // UnmarshalError is a named string

    // xml.CopyToken: a deep copy of a token (StartElement/EndElement/CharData/...).
    public static object? CopyToken(object? t) => t switch
    {
        GoXmlStart st => StartElement_Copy(st),
        GoSlice b => CopyBytes(b),
        _ => t,
    };

    // xml.HTMLAutoClose: the void HTML elements (Go's historical list).
    public static object HTMLAutoClose()
    {
        string[] tags = { "basefont", "br", "area", "link", "img", "param", "hr", "input", "col", "frame", "isindex", "base", "meta" };
        var d = new object?[tags.Length];
        for (int i = 0; i < tags.Length; i++) d[i] = GoString.FromDotNetString(tags[i]);
        return new GoSlice { Data = d, Off = 0, Len = tags.Length, Cap = tags.Length };
    }

    // ---- reflection marshaler ---------------------------------------------
    private static void WriteValue(StringBuilder sb, object? v, string? elem)
    {
        v = Deref(v);
        switch (v)
        {
            case null: if (elem != null) { sb.Append('<').Append(elem).Append("></").Append(elem).Append('>'); } break;
            case bool b: Wrap(sb, elem ?? "bool", b ? "true" : "false"); break;
            case long l: Wrap(sb, elem ?? "int", l.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case int i: Wrap(sb, elem ?? "int", i.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case ulong u: Wrap(sb, elem ?? "uint", u.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case double d: Wrap(sb, elem ?? "float", NumStr(d)); break;
            case GoString gs: Wrap(sb, elem ?? "string", Escape(gs.ToDotNetString())); break;
            case GoSlice s: foreach (var item in Iter(s)) WriteValue(sb, item, elem ?? "string"); break;
            case GoMap m: WriteMap(sb, m, elem ?? "map"); break;
            default: WriteStruct(sb, v, elem); break;
        }
    }

    private static void WriteMap(StringBuilder sb, GoMap m, string elem)
    {
        sb.Append('<').Append(elem).Append('>');
        if (m.Data != null)
        {
            var keys = new List<string>();
            var byKey = new Dictionary<string, object?>();
            foreach (var k in m.Data.Keys)
            {
                string ks = k is GoString g ? g.ToDotNetString() : k?.ToString() ?? "";
                keys.Add(ks); byKey[ks] = m.Data[k];
            }
            keys.Sort(System.StringComparer.Ordinal);
            foreach (var k in keys) WriteValue(sb, byKey[k], k);
        }
        sb.Append("</").Append(elem).Append('>');
    }

    private static void WriteStruct(StringBuilder sb, object v, string? elem)
    {
        var t = v.GetType();
        string name = elem ?? XmlNameOf(t, v) ?? t.Name;
        // Gather attribute fields first.
        var attrs = new StringBuilder();
        var body = new StringBuilder();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.Name.Length == 0 || !char.IsUpper(f.Name[0])) continue;
            if (f.Name == "XMLName") continue;
            string tag = Reflect.TagGet(Reflect.TagFor(t.Name, f.Name), "xml");
            var val = f.GetValue(v);
            string fname = f.Name;
            bool attr = false, chardata = false, omitempty = false, skip = false, innerxml = false;
            if (tag.Length > 0)
            {
                var parts = tag.Split(',');
                if (parts[0] == "-") { skip = true; }
                if (parts[0].Length > 0) fname = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i] == "attr") attr = true;
                    else if (parts[i] == "chardata") chardata = true;
                    else if (parts[i] == "omitempty") omitempty = true;
                    else if (parts[i] == "innerxml") innerxml = true;
                }
            }
            if (skip) continue;
            if (omitempty && IsEmpty(val)) continue;
            if (attr) { attrs.Append(' ').Append(fname).Append("=\"").Append(Escape(Scalar(val))).Append('"'); continue; }
            if (chardata || innerxml) { body.Append(chardata ? Escape(Scalar(val)) : Scalar(val)); continue; }
            WriteValue(body, val, fname);
        }
        sb.Append('<').Append(name).Append(attrs).Append('>').Append(body).Append("</").Append(name).Append('>');
    }

    private static string? XmlNameOf(System.Type t, object v)
    {
        var f = t.GetField("XMLName", BindingFlags.Public | BindingFlags.Instance);
        if (f != null && f.GetValue(v) is GoXmlName n && n.Local.Length > 0) return n.Local;
        // The XMLName tag can name the element even when the field is the zero value.
        if (f != null)
        {
            string tag = Reflect.TagGet(Reflect.TagFor(t.Name, "XMLName"), "xml");
            if (tag.Length > 0) { var p = tag.Split(',')[0]; if (p.Length > 0) return p; }
        }
        return null;
    }

    private static void Wrap(StringBuilder sb, string elem, string text) => sb.Append('<').Append(elem).Append('>').Append(text).Append("</").Append(elem).Append('>');

    private static string Scalar(object? v) => Deref(v) switch
    {
        null => "",
        bool b => b ? "true" : "false",
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ulong u => u.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double d => NumStr(d),
        GoString gs => gs.ToDotNetString(),
        var o => o.ToString() ?? "",
    };

    // ---- indented marshaling (best effort: pretty via re-parse) ------------
    private static void WriteValueIndented(GoXmlEncoder enc, object? v, string? elem)
    {
        var flat = new StringBuilder();
        WriteValue(flat, v, elem);
        enc.Buf.Append(Indent(flat.ToString(), enc.IndentPrefix, enc.IndentStr));
    }

    // Re-indent compact XML: newline + prefix + depth*indent between elements.
    private static string Indent(string xml, string prefix, string indent)
    {
        var sb = new StringBuilder();
        int depth = 0;
        for (int i = 0; i < xml.Length;)
        {
            if (xml[i] == '<')
            {
                int end = xml.IndexOf('>', i);
                if (end < 0) { sb.Append(xml.Substring(i)); break; }
                string tag = xml.Substring(i, end - i + 1);
                bool closing = tag.StartsWith("</");
                bool selfText = false;
                // Peek: <a>text</a> on one line stays compact.
                if (!closing && !tag.EndsWith("/>"))
                {
                    int next = xml.IndexOf('<', end + 1);
                    if (next > end + 1 && xml.Substring(next).StartsWith("</")) selfText = true;
                }
                if (closing) depth--;
                if (sb.Length > 0) { sb.Append('\n').Append(prefix); for (int k = 0; k < depth; k++) sb.Append(indent); }
                sb.Append(tag);
                if (!closing && !tag.EndsWith("/>")) depth++;
                i = end + 1;
                if (selfText)
                {
                    int next = xml.IndexOf('<', i);
                    sb.Append(xml.Substring(i, next - i));
                    int ce = xml.IndexOf('>', next);
                    sb.Append(xml.Substring(next, ce - next + 1));
                    depth--;
                    i = ce + 1;
                }
            }
            else { int next = xml.IndexOf('<', i); if (next < 0) next = xml.Length; sb.Append(xml.Substring(i, next - i)); i = next; }
        }
        return sb.ToString();
    }

    // ---- helpers -----------------------------------------------------------
    private static object? Deref(object? v) => v is GoPtr p ? Deref(p.Value) : v;
    private static IEnumerable<object?> Iter(GoSlice s) { if (s.Data != null) for (int i = 0; i < s.Len; i++) yield return s.Data[s.Off + i]; }
    private static string NumStr(double d) => d == System.Math.Floor(d) && !double.IsInfinity(d)
        ? ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture)
        : d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static string ElemName(GoXmlStart st) => NameStr(st.Name);
    private static string NameStr(GoXmlName n) => n.Local.Length > 0 ? n.Local : "";
    private static string AttrName(GoXmlName n) => n.Local;

    private static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&#34;", '\'' => "&#39;", _ => c.ToString() });
        return sb.ToString();
    }

    private static bool IsEmpty(object? v) => Deref(v) switch
    {
        null => true,
        bool b => !b,
        long l => l == 0,
        int i => i == 0,
        ulong u => u == 0,
        double d => d == 0,
        GoString gs => gs.ToDotNetString().Length == 0,
        GoSlice s => s.Len == 0,
        GoMap m => m.Data == null || m.Data.Count == 0,
        _ => false,
    };

    private static GoSlice Bytes(string s)
    {
        var by = Encoding.UTF8.GetBytes(s);
        var d = new object?[by.Length];
        for (int i = 0; i < by.Length; i++) d[i] = (int)by[i];
        return new GoSlice { Data = d, Off = 0, Len = by.Length, Cap = by.Length };
    }
}

/// <summary>An xml.Decoder (parsing is unsupported under goclr).</summary>
public sealed class GoXmlDecoder { public object? R; }
