namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Text;
using GoCLR.Runtime;

/// <summary>A text/template (or html/template) handle: a parsed node tree plus the
/// shared set of named templates and custom funcs. Execution walks the tree against a
/// "dot" value, resolving fields/keys/methods on goclr runtime values and writing the
/// rendered text to an io.Writer.</summary>
public sealed class GoTemplate
{
    public string Name = "";
    public List<Template.Node>? Root;
    public bool Html; // html/template escapes interpolated values
    public readonly Dictionary<string, GoTemplate> Set = new();
    public readonly Dictionary<string, GoClosure> Funcs = new();
}

/// <summary>Shim for text/template and html/template: a pragmatic engine covering the
/// common surface — text, {{.Field}}/{{.A.B}}/{{.}}, {{$v}}/{{$v := ...}}, pipelines
/// with |, {{if}}/{{range}}/{{with}}/{{else}}/{{end}}, {{template}}/{{define}}, the
/// {{- -}} trim markers, comments, and the builtin functions (printf, len, index,
/// and/or/not, eq/ne/lt/le/gt/ge, print, html, js, urlquery, call).</summary>
public static class Template
{
    // ---- node tree ---------------------------------------------------------
    public abstract class Node { }
    sealed class TextNode : Node { public string Text = ""; }
    sealed class ActionNode : Node { public Pipe Pipe = new(); public string AssignVar = ""; }
    sealed class IfNode : Node { public Pipe Pipe = new(); public List<Node> Then = new(); public List<Node> Else = new(); }
    sealed class RangeNode : Node { public Pipe Pipe = new(); public string KeyVar = "", ValVar = ""; public List<Node> Body = new(); public List<Node> Else = new(); }
    sealed class WithNode : Node { public Pipe Pipe = new(); public List<Node> Body = new(); public List<Node> Else = new(); }
    sealed class TemplateNode : Node { public string Name = ""; public Pipe? Pipe; }

    // A pipeline: command | command | ...  Each command's result feeds the next as its
    // trailing argument. A command is a sequence of arguments; the first decides its kind.
    sealed class Pipe { public List<Cmd> Cmds = new(); }
    sealed class Cmd { public List<Arg> Args = new(); }
    abstract class Arg { }
    sealed class DotArg : Arg { public string[] Path = System.Array.Empty<string>(); } // .A.B (empty = dot)
    sealed class VarArg : Arg { public string Name = ""; public string[] Path = System.Array.Empty<string>(); } // $x.A
    sealed class StrArg : Arg { public string Val = ""; }
    sealed class NumArg : Arg { public object Val = 0L; } // long or double
    sealed class BoolArg : Arg { public bool Val; }
    sealed class NilArg : Arg { }
    sealed class IdentArg : Arg { public string Name = ""; } // a function name
    sealed class PipeArg : Arg { public Pipe Pipe = new(); } // a parenthesized sub-pipeline

    // ---- public shim surface ----------------------------------------------
    // template.IsTrue(val) (truth, ok): does val count as "true" for if/with? Ported from
    // text/template's isTrue (operating on the boxed value rather than a reflect.Value).
    public static object?[] IsTrueFunc(object? val)
    {
        var v = val;
        if (v is GoNamed n) v = n.Value;
        if (v == null) return new object?[] { false, true };       // untyped/typed nil
        switch (v)
        {
            case bool b: return new object?[] { b, true };
            case GoString s: return new object?[] { s.Len > 0, true };
            case GoSlice sl: return new object?[] { sl.Len > 0, true };
            case GoMap m: return new object?[] { m.Data != null && m.Data.Count > 0, true };
            case long l: return new object?[] { l != 0, true };
            case int i: return new object?[] { i != 0, true };
            case ulong u: return new object?[] { u != 0, true };
            case uint ui: return new object?[] { ui != 0, true };
            case double d: return new object?[] { d != 0, true };
            case float f: return new object?[] { f != 0, true };
            case GoComplex c: return new object?[] { c.Re != 0 || c.Im != 0, true };
            case GoPtr p: return new object?[] { p.Value != null || p.Arr != null || p.FGet != null, true }; // nil pointer is false
            case GoClosure: return new object?[] { true, true };    // func
            case GoChan: return new object?[] { true, true };       // chan
        }
        // Anything else is a struct value, which is always true.
        return new object?[] { true, true };
    }

    public static object New(GoString name) => new GoTemplate { Name = name.ToDotNetString() };
    public static object NewHtml(GoString name) => new GoTemplate { Name = name.ToDotNetString(), Html = true };

    public static object? Must(object? t, object? err)
    {
        if (err != null) throw new GoPanicException(((IGoError)err).Error());
        return t;
    }

    public static object Tmpl_New(object t, GoString name)
    {
        var p = (GoTemplate)t;
        var c = new GoTemplate { Name = name.ToDotNetString(), Html = p.Html };
        foreach (var kv in p.Set) c.Set[kv.Key] = kv.Value;
        foreach (var kv in p.Funcs) c.Funcs[kv.Key] = kv.Value;
        return c;
    }
    public static object Tmpl_Delims(object t, GoString left, GoString right) => t; // only default {{ }} supported
    public static object Tmpl_Funcs(object t, GoMap funcMap)
    {
        var g = (GoTemplate)t;
        if (funcMap?.Data != null)
            foreach (var kv in funcMap.Data)
                if (kv.Key is GoString k && kv.Value is GoClosure fn) g.Funcs[k.ToDotNetString()] = fn;
        return t;
    }

    public static object?[] Tmpl_Parse(object t, GoString text)
    {
        var g = (GoTemplate)t;
        try
        {
            var items = Lex(text.ToDotNetString());
            int pos = 0;
            var nodes = ParseList(g, items, ref pos);
            if (pos < items.Count) throw new System.Exception("unexpected " + (items[pos].IsAction ? "{{" + items[pos].Text + "}}" : "text") + " in command");
            g.Root ??= nodes; // the first Parse sets the template's own body
            if (!g.Set.ContainsKey(g.Name)) g.Set[g.Name] = g;
            return new object?[] { t, null };
        }
        catch (System.Exception e)
        {
            return new object?[] { t, new GoError(GoString.FromDotNetString("template: " + e.Message)) };
        }
    }
    public static object?[] Tmpl_ParseFiles(object t, GoSlice files) => new object?[] { t, null };
    public static object?[] Tmpl_ParseGlob(object t, GoString glob) => new object?[] { t, null };

    public static object? Tmpl_Execute(object t, object? w, object? data)
    {
        var g = (GoTemplate)t;
        var sb = new StringBuilder();
        try
        {
            var st = new ExecState { Tmpl = g, Out = sb, Ctx = g.Html ? new HtmlState() : null };
            ExecList(st, g.Root ?? new List<Node>(), data);
            Fmt.WriteTo(w, sb.ToString());
            return null;
        }
        catch (System.Exception e)
        {
            // Go writes output as it executes, so the text produced before an error still
            // reaches the writer; flush the partial render before reporting the error.
            Fmt.WriteTo(w, sb.ToString());
            return new GoError(GoString.FromDotNetString("template: " + e.Message));
        }
    }
    public static object? Tmpl_ExecuteTemplate(object t, object? w, GoString name, object? data)
    {
        var g = (GoTemplate)t;
        if (!g.Set.TryGetValue(name.ToDotNetString(), out var sub))
            return new GoError(GoString.FromDotNetString("template: no template \"" + name.ToDotNetString() + "\""));
        return Tmpl_Execute(MergeSet(g, sub), w, data);
    }

    public static GoSlice Tmpl_Templates(object t)
    {
        var g = (GoTemplate)t;
        var data = new object?[g.Set.Count == 0 ? 1 : g.Set.Count];
        if (g.Set.Count == 0) data[0] = t;
        else { int i = 0; foreach (var v in g.Set.Values) data[i++] = v; }
        return new() { Data = data, Off = 0, Len = data.Length, Cap = data.Length };
    }
    public static GoString Tmpl_Name(object t) => GoString.FromDotNetString(((GoTemplate)t).Name);
    public static object Tmpl_Lookup(object t, GoString name) => ((GoTemplate)t).Set.TryGetValue(name.ToDotNetString(), out var s) ? s : t;
    public static object Tmpl_Option(object t, GoSlice opts) => t;

    // Struct fields whose static type is an html/template trusted-string type, keyed
    // "ClrStructName.FieldName" -> kind ("template.HTML"). Registered at startup so the
    // engine bypasses escaping for them (the field's runtime value is a plain string).
    static readonly Dictionary<string, string> SafeFields = new();
    public static void RegisterSafeField(GoString clrName, GoString field, GoString kind)
        => SafeFields[clrName.ToDotNetString() + "." + field.ToDotNetString()] = kind.ToDotNetString();

    // A value carrying a trusted-string kind it could not carry by type (a struct field
    // read by reflection); SafeKind reads it, and unwrapping yields the underlying value.
    sealed class Tagged { public string Kind = ""; public object? V; }

    private const string UpHex = "0123456789ABCDEF";
    private static bool JsIsSpecial(char r) =>
        r == '\\' || r == '\'' || r == '"' || r == '<' || r == '>' || r == '&' || r == '=' || r < ' ' || r >= 0x80;

    // text/template.JSEscapeString — faithful to Go's JSEscape (dumped vs go run): \ ' "
    // backslash-escape; < > & = use \u00XX (uppercase); other control bytes \u00XX; printable
    // non-ASCII is kept as-is.
    public static GoString JSEscapeString(GoString sS)
    {
        string s = sS.ToDotNetString();
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (!JsIsSpecial(c)) { sb.Append(c); continue; }
            if (c < 0x80)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\\""); break;
                    case '<': sb.Append("\\u003C"); break;
                    case '>': sb.Append("\\u003E"); break;
                    case '&': sb.Append("\\u0026"); break;
                    case '=': sb.Append("\\u003D"); break;
                    default: sb.Append("\\u00").Append(UpHex[(c >> 4) & 0xf]).Append(UpHex[c & 0xf]); break;
                }
            }
            else if (!char.IsControl(c)) sb.Append(c);                 // printable non-ASCII kept
            else sb.Append("\\u").Append(((int)c).ToString("X4"));
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // text/template.HTMLEscapeString — \0 -> U+FFFD, " ' & < > to numeric/named entities.
    public static GoString HTMLEscapeString(GoString sS)
    {
        string s = sS.ToDotNetString();
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch
            {
                '\0' => "�", '"' => "&#34;", '\'' => "&#39;", '&' => "&amp;", '<' => "&lt;", '>' => "&gt;",
                _ => c.ToString(),
            });
        return GoString.FromDotNetString(sb.ToString());
    }

    // The variadic Escaper funcs: format the args (fmt.Sprint) then escape.
    public static GoString HTMLEscaper(GoSlice args) => HTMLEscapeString(Fmt.Sprint(args));
    public static GoString JSEscaper(GoSlice args) => JSEscapeString(Fmt.Sprint(args));
    public static GoString URLQueryEscaper(GoSlice args) => Url.QueryEscape(Fmt.Sprint(args));

    // HTMLEscape(w, b) / JSEscape(w, b): write the escaped bytes to w.
    private static string BytesToStr(GoSlice b)
    {
        var by = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) by[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return System.Text.Encoding.UTF8.GetString(by);
    }
    public static void HTMLEscape(object? w, GoSlice b) => Fmt.WriteTo(w, HTMLEscapeString(GoString.FromDotNetString(BytesToStr(b))).ToDotNetString());
    public static void JSEscape(object? w, GoSlice b) => Fmt.WriteTo(w, JSEscapeString(GoString.FromDotNetString(BytesToStr(b))).ToDotNetString());

    // ---- lexer -------------------------------------------------------------
    sealed class Item { public bool IsAction; public string Text = ""; }

    static List<Item> Lex(string s)
    {
        var items = new List<Item>();
        int i = 0;
        while (i < s.Length)
        {
            int open = s.IndexOf("{{", i, System.StringComparison.Ordinal);
            if (open < 0) { items.Add(new Item { Text = s.Substring(i) }); break; }
            if (open > i) items.Add(new Item { Text = s.Substring(i, open - i) });
            int close = s.IndexOf("}}", open + 2, System.StringComparison.Ordinal);
            if (close < 0) throw new System.Exception("unclosed action");
            string inner = s.Substring(open + 2, close - (open + 2));
            bool trimLeft = inner.StartsWith("-"); if (trimLeft) inner = inner.Substring(1);
            bool trimRight = inner.EndsWith("-"); if (trimRight) inner = inner.Substring(0, inner.Length - 1);
            inner = inner.Trim();
            if (trimLeft && items.Count > 0 && !items[^1].IsAction) items[^1].Text = items[^1].Text.TrimEnd();
            if (!inner.StartsWith("/*")) // skip comments
                items.Add(new Item { IsAction = true, Text = inner });
            i = close + 2;
            if (trimRight && i < s.Length)
            {
                int j = i; while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
                i = j;
            }
        }
        return items;
    }

    // ---- structure parser --------------------------------------------------
    static List<Node> ParseList(GoTemplate g, List<Item> items, ref int pos)
    {
        var nodes = new List<Node>();
        while (pos < items.Count)
        {
            var it = items[pos];
            if (!it.IsAction) { nodes.Add(new TextNode { Text = it.Text }); pos++; continue; }
            string a = it.Text;
            string kw = FirstWord(a);
            if (kw == "end" || kw == "else") return nodes; // handled by caller
            pos++;
            switch (kw)
            {
                case "if": nodes.Add(ParseIf(g, items, ref pos, a)); break;
                case "with": nodes.Add(ParseWith(g, items, ref pos, a)); break;
                case "range": nodes.Add(ParseRange(g, items, ref pos, a)); break;
                case "template": nodes.Add(ParseTemplate(a)); break;
                case "define": ParseDefine(g, items, ref pos, a); break;
                case "block": nodes.Add(ParseBlock(g, items, ref pos, a)); break;
                default: nodes.Add(ParseAction(a)); break;
            }
        }
        return nodes;
    }

    static Node ParseIf(GoTemplate g, List<Item> items, ref int pos, string a)
    {
        var n = new IfNode { Pipe = ParsePipe(a.Substring(2).Trim()), Then = ParseList(g, items, ref pos) };
        if (pos < items.Count && items[pos].IsAction && FirstWord(items[pos].Text) == "else")
        {
            string e = items[pos].Text.Substring(4).Trim();
            pos++;
            if (e.StartsWith("if")) n.Else = new List<Node> { ParseIf(g, items, ref pos, "if " + e.Substring(2).Trim()) };
            else n.Else = ParseList(g, items, ref pos);
        }
        ExpectEnd(items, ref pos);
        return n;
    }

    static Node ParseWith(GoTemplate g, List<Item> items, ref int pos, string a)
    {
        var n = new WithNode { Pipe = ParsePipe(a.Substring(4).Trim()), Body = ParseList(g, items, ref pos) };
        if (pos < items.Count && items[pos].IsAction && FirstWord(items[pos].Text) == "else") { pos++; n.Else = ParseList(g, items, ref pos); }
        ExpectEnd(items, ref pos);
        return n;
    }

    static Node ParseRange(GoTemplate g, List<Item> items, ref int pos, string a)
    {
        string rest = a.Substring(5).Trim();
        string keyVar = "", valVar = "";
        int assign = rest.IndexOf(":=", System.StringComparison.Ordinal);
        if (assign >= 0)
        {
            string lhs = rest.Substring(0, assign).Trim();
            rest = rest.Substring(assign + 2).Trim();
            var parts = lhs.Split(',');
            if (parts.Length == 1) valVar = parts[0].Trim().TrimStart('$');
            else { keyVar = parts[0].Trim().TrimStart('$'); valVar = parts[1].Trim().TrimStart('$'); }
        }
        var n = new RangeNode { Pipe = ParsePipe(rest), KeyVar = keyVar, ValVar = valVar, Body = ParseList(g, items, ref pos) };
        if (pos < items.Count && items[pos].IsAction && FirstWord(items[pos].Text) == "else") { pos++; n.Else = ParseList(g, items, ref pos); }
        ExpectEnd(items, ref pos);
        return n;
    }

    static void ParseDefine(GoTemplate g, List<Item> items, ref int pos, string a)
    {
        string name = Unquote(a.Substring(6).Trim());
        var body = ParseList(g, items, ref pos);
        ExpectEnd(items, ref pos);
        g.Set[name] = new GoTemplate { Name = name, Html = g.Html, Root = body };
    }

    static Node ParseBlock(GoTemplate g, List<Item> items, ref int pos, string a)
    {
        string rest = a.Substring(5).Trim();
        string name = Unquote(FirstWord(rest));
        string pipeStr = rest.Substring(FirstWord(rest).Length).Trim();
        var body = ParseList(g, items, ref pos);
        ExpectEnd(items, ref pos);
        g.Set[name] = new GoTemplate { Name = name, Html = g.Html, Root = body };
        return new TemplateNode { Name = name, Pipe = pipeStr.Length > 0 ? ParsePipe(pipeStr) : null };
    }

    static Node ParseTemplate(string a)
    {
        string rest = a.Substring(8).Trim();
        string name = Unquote(FirstWord(rest));
        string pipeStr = rest.Substring(FirstWord(rest).Length).Trim();
        return new TemplateNode { Name = name, Pipe = pipeStr.Length > 0 ? ParsePipe(pipeStr) : null };
    }

    static Node ParseAction(string a)
    {
        var m = System.Text.RegularExpressions.Regex.Match(a, @"^\$(\w+)\s*:?=\s*(.+)$");
        if (m.Success) return new ActionNode { AssignVar = m.Groups[1].Value, Pipe = ParsePipe(m.Groups[2].Value) };
        return new ActionNode { Pipe = ParsePipe(a) };
    }

    static void ExpectEnd(List<Item> items, ref int pos)
    {
        if (pos < items.Count && items[pos].IsAction && FirstWord(items[pos].Text) == "end") pos++;
        else throw new System.Exception("missing {{end}}");
    }

    static string FirstWord(string s) { int i = 0; while (i < s.Length && !char.IsWhiteSpace(s[i])) i++; return s.Substring(0, i); }

    // ---- pipeline parser ---------------------------------------------------
    static Pipe ParsePipe(string s)
    {
        var pipe = new Pipe();
        foreach (var seg in SplitTop(s, '|'))
        {
            var cmd = new Cmd();
            foreach (var tok in Tokenize(seg.Trim())) cmd.Args.Add(ParseArg(tok));
            if (cmd.Args.Count > 0) pipe.Cmds.Add(cmd);
        }
        return pipe;
    }

    static Arg ParseArg(string tok)
    {
        if (tok.Length == 0) return new NilArg();
        if (tok[0] == '(' && tok[^1] == ')') return new PipeArg { Pipe = ParsePipe(tok.Substring(1, tok.Length - 2)) };
        if (tok[0] == '"' || tok[0] == '`') return new StrArg { Val = Unquote(tok) };
        if (tok[0] == '\'') { string u = Unquote(tok); return new NumArg { Val = (long)(u.Length > 0 ? u[0] : 0) }; }
        if (tok == "nil") return new NilArg();
        if (tok == "true") return new BoolArg { Val = true };
        if (tok == "false") return new BoolArg { Val = false };
        if (tok[0] == '$')
        {
            var parts = tok.Substring(1).Split('.');
            return new VarArg { Name = parts[0], Path = parts.Length > 1 ? parts[1..] : System.Array.Empty<string>() };
        }
        if (tok[0] == '.')
        {
            if (tok == ".") return new DotArg();
            return new DotArg { Path = tok.Substring(1).Split('.') };
        }
        if (char.IsDigit(tok[0]) || (tok[0] == '-' && tok.Length > 1 && char.IsDigit(tok[1])))
        {
            if (tok.Contains('.') || tok.Contains('e') || tok.Contains('E')) return new NumArg { Val = double.Parse(tok, System.Globalization.CultureInfo.InvariantCulture) };
            return new NumArg { Val = long.Parse(tok, System.Globalization.CultureInfo.InvariantCulture) };
        }
        return new IdentArg { Name = tok };
    }

    // ---- executor ----------------------------------------------------------
    sealed class ExecState { public GoTemplate Tmpl = null!; public StringBuilder Out = null!; public readonly Dictionary<string, object?> Vars = new(); public HtmlState? Ctx; }

    static void ExecList(ExecState st, List<Node> nodes, object? dot) { foreach (var n in nodes) ExecNode(st, n, dot); }

    static void ExecNode(ExecState st, Node n, object? dot)
    {
        switch (n)
        {
            case TextNode t: st.Out.Append(t.Text); st.Ctx?.Feed(t.Text); break;
            case ActionNode a:
                {
                    var v = EvalPipe(st, a.Pipe, dot);
                    if (a.AssignVar.Length > 0) { st.Vars[a.AssignVar] = v; break; }
                    string outp = st.Ctx == null ? Render(v, false) : EscapeFor(st.Ctx, v);
                    st.Out.Append(outp);
                    st.Ctx?.Feed(outp);
                    break;
                }
            case IfNode iff:
                if (IsTrue(EvalPipe(st, iff.Pipe, dot))) ExecList(st, iff.Then, dot);
                else ExecList(st, iff.Else, dot);
                break;
            case WithNode w:
                {
                    var v = EvalPipe(st, w.Pipe, dot);
                    if (IsTrue(v)) ExecList(st, w.Body, v);
                    else ExecList(st, w.Else, dot);
                    break;
                }
            case RangeNode r: ExecRange(st, r, dot); break;
            case TemplateNode tn:
                {
                    var arg = tn.Pipe != null ? EvalPipe(st, tn.Pipe, dot) : dot;
                    if (st.Tmpl.Set.TryGetValue(tn.Name, out var sub))
                    {
                        var sub2 = new ExecState { Tmpl = MergeSet(st.Tmpl, sub), Out = st.Out, Ctx = st.Ctx };
                        ExecList(sub2, sub.Root ?? new List<Node>(), arg);
                    }
                    break;
                }
        }
    }

    static GoTemplate MergeSet(GoTemplate root, GoTemplate sub)
    {
        foreach (var kv in root.Set) if (!sub.Set.ContainsKey(kv.Key)) sub.Set[kv.Key] = kv.Value;
        foreach (var kv in root.Funcs) if (!sub.Funcs.ContainsKey(kv.Key)) sub.Funcs[kv.Key] = kv.Value;
        sub.Html = root.Html;
        return sub;
    }

    static void ExecRange(ExecState st, RangeNode r, object? dot)
    {
        var coll = Deref(EvalPipe(st, r.Pipe, dot));
        bool any = false;
        void Iter(object? key, object? val)
        {
            any = true;
            if (r.KeyVar.Length > 0) st.Vars[r.KeyVar] = key;
            if (r.ValVar.Length > 0) st.Vars[r.ValVar] = val;
            ExecList(st, r.Body, val);
        }
        switch (coll)
        {
            case GoSlice s:
                if (s.Data != null) for (int i = 0; i < s.Len; i++) Iter((long)i, s.Data[s.Off + i]);
                break;
            case GoMap m when m.Data != null:
                {
                    var keys = new List<object>(m.Data.Keys);
                    keys.Sort((x, y) => string.CompareOrdinal(KeyStr(x), KeyStr(y)));
                    foreach (var k in keys) Iter(k, m.Data[k]);
                    break;
                }
            case long n: for (long i = 0; i < n; i++) Iter(i, i); break;
            case int ni: for (int i = 0; i < ni; i++) Iter((long)i, (long)i); break;
        }
        if (!any) ExecList(st, r.Else, dot);
    }

    static string KeyStr(object? k) => k is GoString g ? g.ToDotNetString() : k?.ToString() ?? "";

    static object? EvalPipe(ExecState st, Pipe p, object? dot)
    {
        object? result = null;
        for (int ci = 0; ci < p.Cmds.Count; ci++)
            result = EvalCmd(st, p.Cmds[ci], dot, result, ci > 0);
        return result;
    }

    static object? EvalCmd(ExecState st, Cmd cmd, object? dot, object? piped, bool hasPiped)
    {
        var head = cmd.Args[0];
        if (head is IdentArg id)
        {
            var args = new List<object?>();
            for (int i = 1; i < cmd.Args.Count; i++) args.Add(EvalArg(st, cmd.Args[i], dot));
            if (hasPiped) args.Add(piped);
            return CallFunc(st, id.Name, args, dot);
        }
        return EvalArg(st, head, dot);
    }

    static object? EvalArg(ExecState st, Arg a, object? dot)
    {
        switch (a)
        {
            case DotArg d: return d.Path.Length == 0 ? dot : ResolvePath(dot, d.Path);
            case VarArg v:
                {
                    st.Vars.TryGetValue(v.Name, out var val);
                    return v.Path.Length == 0 ? val : ResolvePath(val, v.Path);
                }
            case StrArg s: return GoString.FromDotNetString(s.Val);
            case NumArg n: return n.Val;
            case BoolArg b: return b.Val;
            case NilArg: return null;
            case PipeArg pa: return EvalPipe(st, pa.Pipe, dot);
            case IdentArg id: return CallFunc(st, id.Name, new List<object?>(), dot);
        }
        return null;
    }

    static object? ResolvePath(object? v, string[] path) { foreach (var name in path) v = Field(v, name); return v; }

    static object? Field(object? v, string name)
    {
        v = Deref(v);
        if (v == null) return null;
        if (v is GoMap m) return GoMaps.Get(m, GoString.FromDotNetString(name), null);
        var f = v.GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            var val = f.GetValue(v);
            // A field of an html/template trusted-string type is tagged so the escaper
            // can recognize it (its runtime value is an indistinguishable string).
            if (SafeFields.TryGetValue(v.GetType().Name + "." + name, out var kind)) return new Tagged { Kind = kind, V = val };
            return val;
        }
        if (Bridge.HasMethod(v, name)) return Bridge.CallMethod(v, name);
        throw new System.Exception("can't evaluate field " + name);
    }

    static object? Deref(object? v)
    {
        if (v is Tagged tg) v = tg.V;
        v = Rt.Unwrap(v);
        if (v is GoPtr p) v = Deref(GoPtrs.Get(p));
        return v;
    }

    // ---- builtin + custom functions ---------------------------------------
    static object? CallFunc(ExecState st, string name, List<object?> args, object? dot)
    {
        if (st.Tmpl.Funcs.TryGetValue(name, out var fn))
        {
            var r = GoRuntime.InvokeArgs(fn, args.ToArray());
            return r is object?[] tup && tup.Length > 0 ? tup[0] : r; // (value, error) -> value
        }
        switch (name)
        {
            case "printf": return GoString.FromDotNetString(Fmt.Sprintf(Str(args[0]), SliceOf(args.GetRange(1, args.Count - 1))).ToDotNetString());
            case "print": return Fmt.Sprint(SliceOf(args));
            case "println": return Fmt.Sprintln(SliceOf(args));
            case "len": return (long)Len(args[0]);
            case "index": return Index(args);
            case "and": { object? r = args.Count > 0 ? args[0] : null; foreach (var x in args) { r = x; if (!IsTrue(x)) break; } return r; }
            case "or": { object? r = args.Count > 0 ? args[0] : null; foreach (var x in args) { r = x; if (IsTrue(x)) break; } return r; }
            case "not": return !IsTrue(args[0]);
            case "eq": return Eq(args[0], args[1]);
            case "ne": return !Eq(args[0], args[1]);
            case "lt": return Cmp(args[0], args[1]) < 0;
            case "le": return Cmp(args[0], args[1]) <= 0;
            case "gt": return Cmp(args[0], args[1]) > 0;
            case "ge": return Cmp(args[0], args[1]) >= 0;
            case "html": return GoString.FromDotNetString(HtmlEscape(Render(args[0], false)));
            case "js": return JSEscapeString(Str(args[0]));
            case "urlquery": return GoString.FromDotNetString(System.Uri.EscapeDataString(Render(args[0], false)));
            case "call": { var fn2 = args[0] as GoClosure; return fn2 != null ? GoRuntime.InvokeArgs(fn2, args.GetRange(1, args.Count - 1).ToArray()) : null; }
            default: throw new System.Exception("function \"" + name + "\" not defined");
        }
    }

    static GoString Str(object? v) => v is GoString gs ? gs : GoString.FromDotNetString(Render(v, false));
    static GoSlice SliceOf(List<object?> a) => new() { Data = a.ToArray(), Off = 0, Len = a.Count, Cap = a.Count };

    static int Len(object? v)
    {
        v = Deref(v);
        return v switch { GoString g => g.Len, GoSlice s => s.Len, GoMap m => (int)GoMaps.Len(m), _ => 0 };
    }

    static object? Index(List<object?> a)
    {
        object? v = a[0];
        for (int i = 1; i < a.Count; i++)
        {
            v = Deref(v);
            if (v is GoSlice s) { long k = ToL(a[i]); v = (k >= 0 && k < s.Len) ? s.Data![s.Off + (int)k] : null; }
            else if (v is GoMap m) v = GoMaps.Get(m, a[i], null); // map key used as-is (not numeric)
            else if (v is GoString g) { long k = ToL(a[i]); var b = g.Bytes; v = (k >= 0 && k < b.Length) ? (long)b[(int)k] : null; }
            else return null;
        }
        return v;
    }

    static string Render(object? v, bool html)
    {
        if (v is Tagged tg) v = tg.V;
        v = Rt.Unwrap(v);
        string s = v == null ? "<no value>" : Fmt.Sprint(SliceOf(new List<object?> { v })).ToDotNetString();
        return html ? HtmlEscape(s) : s;
    }

    static string HtmlEscape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch { '<' => "&lt;", '>' => "&gt;", '&' => "&amp;", '\'' => "&#39;", '"' => "&#34;", _ => c.ToString() });
        return sb.ToString();
    }

    // ---- html/template contextual auto-escaping ----------------------------
    enum Ctx { Text, TagName, BeforeAttr, AttrName, AfterAttrName, BeforeValue, ValueDQ, ValueSQ, ValueUQ, Js, JsStrDQ, JsStrSQ, Css, Comment }

    // A lightweight HTML tokenizer that tracks the context of the output produced so far,
    // so each interpolated value gets the escaper its position requires (text/attr/URL/
    // JS/CSS) — html/template's contextual auto-escaping, computed at execution time.
    sealed class HtmlState
    {
        public Ctx C = Ctx.Text;
        public string Tag = "", Attr = "";
        public bool UrlQuery; // within a URL attribute value, true once past the '?' (query/fragment part)
        readonly StringBuilder tag = new(), attr = new();
        readonly StringBuilder tail = new(); // rolling lowercased suffix, for </script> etc.

        public void Feed(string s) { foreach (char c in s) Step(c); }

        void Step(char c)
        {
            tail.Append(char.ToLowerInvariant(c));
            if (tail.Length > 10) tail.Remove(0, tail.Length - 10);
            switch (C)
            {
                case Ctx.Text:
                    if (c == '<') { tag.Clear(); C = Ctx.TagName; }
                    break;
                case Ctx.TagName:
                    if (c == '!' && tag.Length == 0) { C = Ctx.Comment; }
                    else if (char.IsWhiteSpace(c)) { Tag = tag.ToString().ToLowerInvariant(); C = Ctx.BeforeAttr; }
                    else if (c == '>') { Tag = tag.ToString().ToLowerInvariant(); EnterContent(); }
                    else if (c == '/') { /* closing or self-close */ }
                    else tag.Append(c);
                    break;
                case Ctx.BeforeAttr:
                    if (c == '>') EnterContent();
                    else if (c == '/' || char.IsWhiteSpace(c)) { }
                    else { attr.Clear(); attr.Append(c); C = Ctx.AttrName; }
                    break;
                case Ctx.AttrName:
                    if (c == '=') { Attr = attr.ToString().ToLowerInvariant(); C = Ctx.BeforeValue; UrlQuery = false; }
                    else if (char.IsWhiteSpace(c)) { Attr = attr.ToString().ToLowerInvariant(); C = Ctx.AfterAttrName; }
                    else if (c == '>') { EnterContent(); }
                    else attr.Append(c);
                    break;
                case Ctx.AfterAttrName:
                    if (c == '=') { C = Ctx.BeforeValue; UrlQuery = false; }
                    else if (char.IsWhiteSpace(c)) { }
                    else if (c == '>') EnterContent();
                    else { attr.Clear(); attr.Append(c); C = Ctx.AttrName; }
                    break;
                case Ctx.BeforeValue:
                    if (char.IsWhiteSpace(c)) { }
                    else if (c == '"') C = Ctx.ValueDQ;
                    else if (c == '\'') C = Ctx.ValueSQ;
                    else if (c == '>') EnterContent();
                    else C = Ctx.ValueUQ;
                    break;
                case Ctx.ValueDQ: if (c == '?') UrlQuery = true; if (c == '"') C = Ctx.BeforeAttr; break;
                case Ctx.ValueSQ: if (c == '?') UrlQuery = true; if (c == '\'') C = Ctx.BeforeAttr; break;
                case Ctx.ValueUQ: if (c == '?') UrlQuery = true; if (char.IsWhiteSpace(c)) C = Ctx.BeforeAttr; else if (c == '>') EnterContent(); break;
                case Ctx.Js: if (c == '"') C = Ctx.JsStrDQ; else if (c == '\'') C = Ctx.JsStrSQ; else if (c == '<') { /* maybe </script> */ } break;
                case Ctx.JsStrDQ: if (c == '"') C = Ctx.Js; break;
                case Ctx.JsStrSQ: if (c == '\'') C = Ctx.Js; break;
                case Ctx.Css: break;
                case Ctx.Comment: break;
            }
            // crude end-of-special-element detection for </script> / </style> / -->
            if (C == Ctx.Js && EndsWith("</script")) C = Ctx.Text;
            else if (C == Ctx.Css && EndsWith("</style")) C = Ctx.Text;
            else if (C == Ctx.Comment && EndsWith("-->")) C = Ctx.Text;
        }

        bool EndsWith(string s)
        {
            if (tail.Length < s.Length) return false;
            for (int i = 0; i < s.Length; i++) if (tail[tail.Length - s.Length + i] != s[i]) return false;
            return true;
        }

        void EnterContent()
        {
            C = Tag switch { "script" => Ctx.Js, "style" => Ctx.Css, _ => Ctx.Text };
            Attr = "";
        }
    }

    static readonly HashSet<string> UrlAttrs = new() { "href", "src", "action", "formaction", "cite", "background", "poster", "longdesc", "usemap", "data" };

    // The html/template trusted-string type carried by a tagged value (or null).
    static string? SafeKind(object? v) => v is Tagged t ? t.Kind : v is GoNamed n ? Rt.NamedTypeName(n.TypeId) : null;

    static string EscapeFor(HtmlState s, object? v)
    {
        string? safe = SafeKind(v);
        string raw = Render(v, false); // the underlying string (Render unwraps GoNamed)
        bool url = UrlAttrs.Contains(s.Attr);
        bool ev = IsEventAttr(s.Attr); // onclick=, onload=, ... — the value is a JS context
        bool css = s.Attr == "style"; // style="..." — the value is a CSS context
        switch (s.C)
        {
            case Ctx.Js:
            case Ctx.JsStrDQ:
            case Ctx.JsStrSQ:
                if (safe == "template.JS") return raw;
                if ((s.C == Ctx.JsStrDQ || s.C == Ctx.JsStrSQ) && safe == "template.JSStr") return raw;
                return JsValue(v, s.C);
            case Ctx.Css:
                return safe == "template.CSS" ? raw : CssValueFilter(raw);
            case Ctx.BeforeValue: // an action right after `=` is the (unquoted) value
            case Ctx.ValueUQ:
                if (url) return UqAttrEscape(UrlForPart(raw, s.UrlQuery, safe == "template.URL"));
                if (ev) return UqAttrEscape(safe == "template.JS" ? raw : JsValue(v, Ctx.Js));
                if (css) return UqAttrEscape(safe == "template.CSS" ? raw : CssValueFilter(raw));
                if (safe == "template.HTMLAttr") return raw;
                return UqAttrEscape(raw);
            case Ctx.ValueDQ:
            case Ctx.ValueSQ:
                if (url) return HtmlEscape(UrlForPart(raw, s.UrlQuery, safe == "template.URL"));
                if (ev) return HtmlEscape(safe == "template.JS" ? raw : JsValue(v, Ctx.Js));
                if (css) return HtmlEscape(safe == "template.CSS" ? raw : CssValueFilter(raw));
                if (safe == "template.HTMLAttr") return raw;
                return HtmlEscape(raw);
            default:
                return safe == "template.HTML" ? raw : HtmlEscape(raw);
        }
    }

    // An event-handler attribute (onclick, onload, onmouseover, ...) carries JavaScript.
    static bool IsEventAttr(string a) => a.Length > 2 && a[0] == 'o' && a[1] == 'n';

    // Picks the URL escaper for the part the value lands in: a trusted template.URL is only
    // normalized; an untrusted value in the query/fragment is fully percent-escaped (so '&'
    // can't inject a parameter), while one in the scheme/path is normalized (reserved chars
    // such as '&' kept, then HTML-escaped by the caller).
    static string UrlForPart(string raw, bool inQuery, bool trusted)
    {
        if (trusted) return UrlNormalize(raw, true);
        return inQuery ? UrlEscapeQuery(raw) : UrlNormalize(raw, false);
    }

    // A value interpolated into JS: a string becomes a quoted, escaped JS string literal
    // (or, inside an existing JS string, just the escaped body); numbers/bools their
    // literal; anything else its JSON-ish form.
    static string JsValue(object? v, Ctx c)
    {
        v = Rt.Unwrap(v);
        bool inStr = c == Ctx.JsStrDQ || c == Ctx.JsStrSQ;
        // Inside an existing JS string literal: just the escaped body.
        if (inStr) return JsStrEscape(v is GoString gg ? gg.ToDotNetString() : Render(v, false));
        // A bare JS expression: a string becomes a quoted JSON string literal (Go marshals
        // it — note '/' is NOT escaped there, unlike inside an existing JS string); a
        // number/bool/null is space-padded so it can't merge with adjacent tokens.
        if (v is GoString g) return JsValEscape(g.ToDotNetString());
        if (v == null) return " null ";
        if (v is bool b) return b ? " true " : " false ";
        if (IsNum(v)) return " " + Render(v, false) + " ";
        return JsValEscape(Render(v, false));
    }

    // The body of a JS string literal: Go's jsStrEscaper, which uses \uXXXX for quotes and
    // HTML-significant runes and escapes '/' as '\/' (so a value inside "..." cannot close a
    // surrounding </script>).
    static string JsStrEscape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch
            {
                '\\' => "\\\\", '"' => "\\u0022", '\'' => "\\u0027", '/' => "\\/", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                '<' => "\\u003c", '>' => "\\u003e", '&' => "\\u0026", '=' => "\\u003d", '`' => "\\u0060", '+' => "\\u002b",
                _ => c.ToString(),
            });
        return sb.ToString();
    }

    // A bare JS value: Go marshals it to JSON, then replaces HTML-significant runes with their
    // \uXXXX form. Standard JSON escapes apply ('"' -> \", '\\' -> \\\\, control chars), and
    // crucially '/' is left as-is. The result is wrapped in double quotes.
    static string JsValEscape(string s)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (char c in s)
            sb.Append(c switch
            {
                '\\' => "\\\\", '"' => "\\\"", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                '<' => "\\u003c", '>' => "\\u003e", '&' => "\\u0026",
                '\u2028' => "\\u2028", '\u2029' => "\\u2029",
                _ => c < 0x20 ? "\\u" + ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture) : c.ToString(),
            });
        sb.Append('"');
        return sb.ToString();
    }

    // The query/fragment part of a URL (Go's urlEscaper): everything except the RFC 3986
    // unreserved set is percent-encoded as UTF-8 bytes, so '&', ' ', '=', '?' all escape.
    static string UrlEscapeQuery(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                c == '-' || c == '.' || c == '_' || c == '~')
                sb.Append(c);
            else
                foreach (byte b in System.Text.Encoding.UTF8.GetBytes(c.ToString()))
                    sb.Append('%').Append(((int)b).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    // Go's cssValueFilter: an interpolated CSS value is passed through only if it can't break
    // out of its declaration; any structural/quote/comment rune (or "--") yields the failsafe
    // "ZgotmplZ". Quantities (10px, 25%), keywords, hex colors, and space/':'/',' pass.
    static string CssValueFilter(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\0' || c == '"' || c == '\'' || c == '(' || c == ')' || c == '/' ||
                c == ';' || c == '@' || c == '[' || c == '\\' || c == ']' || c == '`' ||
                c == '{' || c == '}')
                return "ZgotmplZ";
            if (c == '-' && i != 0 && s[i - 1] == '-') return "ZgotmplZ";
        }
        return s;
    }

    static string UqAttrEscape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(c switch
            {
                '<' => "&lt;", '>' => "&gt;", '&' => "&amp;", '"' => "&#34;", '\'' => "&#39;",
                ' ' => "&#32;", '\t' => "&#9;", '\n' => "&#10;", '\r' => "&#13;", '\f' => "&#12;",
                '=' => "&#61;", '`' => "&#96;",
                _ => c.ToString(),
            });
        return sb.ToString();
    }

    static string CssEscape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c == '<' || c == '>' || c == '"' || c == '\'' || c == '\\' || c == '&' || c == '(' || c == ')' || c == '/' || c == ':' || c == ';' || c == '{' || c == '}')
                sb.Append('\\').Append(((int)c).ToString("x", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // A full URL: block dangerous schemes (javascript:, vbscript:, data:) with Go's
    // #ZgotmplZ marker, otherwise %-encode the few characters that must be escaped.
    static string UrlNormalize(string s, bool trusted = false)
    {
        if (!trusted)
        {
            string trimmed = s.TrimStart();
            int colon = trimmed.IndexOf(':');
            if (colon > 0)
            {
                string scheme = trimmed.Substring(0, colon).ToLowerInvariant();
                if (System.Array.IndexOf(new[] { "javascript", "vbscript", "data" }, scheme) >= 0 && !scheme.Contains('/'))
                    return "#ZgotmplZ";
            }
        }
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            switch (c)
            {
                case ' ': sb.Append("%20"); break;
                case '"': sb.Append("%22"); break;
                case '\'': sb.Append("%27"); break;
                case '(': sb.Append("%28"); break;
                case ')': sb.Append("%29"); break;
                case '<': sb.Append("%3c"); break;
                case '>': sb.Append("%3e"); break;
                case '`': sb.Append("%60"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    static bool IsTrue(object? v)
    {
        if (v is Tagged tg) v = tg.V;
        v = Rt.Unwrap(v);
        return v switch
        {
            null => false,
            bool b => b,
            GoString g => g.Len > 0,
            long n => n != 0,
            int ni => ni != 0,
            ulong u => u != 0,
            uint ui => ui != 0,
            double d => d != 0,
            float f => f != 0,
            GoSlice s => s.Len > 0,
            GoMap m => GoMaps.Len(m) > 0,
            GoPtr p => GoPtrs.Get(p) != null || p.TypeId != 0,
            _ => true,
        };
    }

    // Go's basic-kind classes for eq/lt/...: a comparison across different kinds (e.g.
    // int vs float, or string vs number) is an error, matching text/template.
    static int BasicKind(object? v) => v switch
    {
        bool => 1,
        long or int or short or sbyte => 2,
        ulong or uint or ushort or byte => 3,
        double or float => 4,
        GoString => 5,
        _ => 0,
    };

    static bool Eq(object? a, object? b)
    {
        a = Deref(a); b = Deref(b);
        int ka = BasicKind(a), kb = BasicKind(b);
        if (ka != 0 && kb != 0 && ka != kb) throw new System.Exception("incompatible types for comparison");
        if (a is GoString x && b is GoString y) return GoStrings.Equal(x, y);
        if (IsNum(a) && IsNum(b)) return ToD(a) == ToD(b);
        if (a is bool ba && b is bool bb) return ba == bb;
        return System.Object.Equals(a, b);
    }

    static int Cmp(object? a, object? b)
    {
        a = Deref(a); b = Deref(b);
        int ka = BasicKind(a), kb = BasicKind(b);
        if (ka != 0 && kb != 0 && ka != kb) throw new System.Exception("incompatible types for comparison");
        if (a is GoString x && b is GoString y) return string.CompareOrdinal(x.ToDotNetString(), y.ToDotNetString());
        return ToD(a).CompareTo(ToD(b));
    }

    static bool IsNum(object? v) => v is long or int or ulong or uint or double or float or short or ushort or byte or sbyte;
    static double ToD(object? v) => v == null ? 0 : System.Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
    static long ToL(object? v) => v == null ? 0 : System.Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture);

    // ---- token helpers -----------------------------------------------------
    static IEnumerable<string> SplitTop(string s, char sep)
    {
        int depth = 0; var cur = new StringBuilder(); char q = '\0';
        foreach (char c in s)
        {
            if (q != '\0') { cur.Append(c); if (c == q) q = '\0'; continue; }
            if (c == '"' || c == '\'' || c == '`') { q = c; cur.Append(c); continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == sep && depth == 0) { yield return cur.ToString(); cur.Clear(); continue; }
            cur.Append(c);
        }
        yield return cur.ToString();
    }

    static List<string> Tokenize(string s)
    {
        var toks = new List<string>(); var cur = new StringBuilder(); char q = '\0'; int depth = 0;
        foreach (char c in s)
        {
            if (q != '\0') { cur.Append(c); if (c == q) q = '\0'; continue; }
            if (c == '"' || c == '\'' || c == '`') { q = c; cur.Append(c); continue; }
            if (c == '(') { depth++; cur.Append(c); continue; }
            if (c == ')') { depth--; cur.Append(c); continue; }
            if (depth == 0 && char.IsWhiteSpace(c)) { if (cur.Length > 0) { toks.Add(cur.ToString()); cur.Clear(); } continue; }
            cur.Append(c);
        }
        if (cur.Length > 0) toks.Add(cur.ToString());
        return toks;
    }

    static string Unquote(string s)
    {
        if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'' || s[0] == '`') && s[^1] == s[0])
        {
            s = s.Substring(1, s.Length - 2);
            if (s.Contains('\\')) s = s.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
        return s;
    }
}
