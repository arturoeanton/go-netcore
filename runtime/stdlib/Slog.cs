namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Text;
using GoCLR.Runtime;

/// <summary>A slog.Handler: a writer plus the text/JSON choice and a minimum level.</summary>
public sealed class GoSlogHandler
{
    public object? Writer;
    public bool Json;
    public long Level; // slog.LevelInfo == 0
    public readonly List<GoSlogGAttr> Preset = new();
    public readonly List<string> Groups = new(); // open groups (WithGroup), applied to later attrs
}

/// <summary>An accumulated attribute plus the group path open when it was added, so the
/// renderer can nest it (JSON) or dotted-prefix it (text) like slog's WithGroup.</summary>
public sealed class GoSlogGAttr
{
    public List<string> Groups = new();
    public string Key = "";
    public object? Value;
}

/// <summary>A *slog.Logger over a handler.</summary>
public sealed class GoSlogLogger { public GoSlogHandler Handler = null!; }

/// <summary>A slog.Attr (key + value), produced by slog.String/Int/Any/… .</summary>
public sealed class GoSlogAttr { public string Key = ""; public object? Value; }

/// <summary>A slog.HandlerOptions: the output omits the timestamp/source unconditionally,
/// so the options are accepted (to compile real configs) but not otherwise applied.</summary>
public sealed class GoSlogHandlerOptions { }

/// <summary>Shim for a subset of log/slog. Records carry level, message and attributes
/// (key/value pairs or slog.Attr); the automatic timestamp is omitted so output is
/// reproducible — configure a ReplaceAttr that drops slog.TimeKey to match under go.</summary>
public static class Slog
{
    public static long LevelDebug() => -4;
    public static long LevelInfo() => 0;
    public static long LevelWarn() => 4;
    public static long LevelError() => 8;

    private static string LevelName(long lvl) => lvl switch
    {
        <= -4 => "DEBUG",
        <= 0 => "INFO",
        <= 4 => "WARN",
        _ => "ERROR",
    };

    // (slog.Level).String(): base name plus a signed offset for non-canonical values
    // ("INFO", "INFO+2", "WARN-1", "DEBUG+4"), matching Go exactly.
    public static GoString Level_String(long l)
    {
        static string Str(string b, long v) => v == 0 ? b : b + (v >= 0 ? "+" : "") + v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string s = l < 0 ? Str("DEBUG", l + 4)
                 : l < 4 ? Str("INFO", l)
                 : l < 8 ? Str("WARN", l - 4)
                 : Str("ERROR", l - 8);
        return GoString.FromDotNetString(s);
    }
    // (slog.Level).Level(): a Level is its own Leveler.
    public static long Level_Level(long l) => l;

    // --- handlers / loggers --------------------------------------------------
    public static object NewTextHandler(object? w, object? opts) => new GoSlogHandler { Writer = w, Json = false };
    public static object NewJSONHandler(object? w, object? opts) => new GoSlogHandler { Writer = w, Json = true };
    public static object New(object? handler) => new GoSlogLogger { Handler = (GoSlogHandler)handler! };

    private static GoSlogLogger? _default;
    private static GoSlogLogger Default() => _default ??= new GoSlogLogger { Handler = new GoSlogHandler { Writer = Os.Stderr() } };
    public static object DefaultLogger() => Default();
    public static void SetDefault(object? l) { _default = (GoSlogLogger)l!; }

    // record-key constants
    public static GoString KeyTime() => GoString.FromDotNetString("time");
    public static GoString KeyMessage() => GoString.FromDotNetString("msg");
    public static GoString KeyLevel() => GoString.FromDotNetString("level");
    public static GoString KeySource() => GoString.FromDotNetString("source");

    // slog.Attr accessors and zero value (slog.Attr{}).
    public static object NewAttr() => new GoSlogAttr();
    public static GoString Attr_Key(object a) => GoString.FromDotNetString(((GoSlogAttr)a).Key);
    public static object? Attr_Value(object a) => ((GoSlogAttr)a).Value;

    // slog.HandlerOptions zero value and (no-op) field setters.
    public static object NewHandlerOptions() => new GoSlogHandlerOptions();
    public static void HO_SetLevel(object o, object? v) { }
    public static void HO_SetAddSource(object o, bool v) { }
    public static void HO_SetReplaceAttr(object o, GoClosure? f) { }

    // --- attribute constructors ---------------------------------------------
    public static object String(GoString k, GoString v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Int(GoString k, long v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Int64(GoString k, long v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Uint64(GoString k, ulong v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Float64(GoString k, double v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Bool(GoString k, bool v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Any(GoString k, object? v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = v };
    public static object Duration(GoString k, long v) => new GoSlogAttr { Key = k.ToDotNetString(), Value = Time.Duration_String(v) };
    // slog.Group(key, args...): an Attr whose value is a sub-group of attrs; rendered nested
    // (JSON) or dotted (text) under key. The children are pre-collected with an empty relative
    // group path and expanded under key at collect time.
    public static object Group(GoString key, GoSlice args)
    {
        var children = new List<GoSlogGAttr>();
        CollectArgs(children, args, new List<string>());
        return new GoSlogAttr { Key = key.ToDotNetString(), Value = children };
    }

    // --- logging -------------------------------------------------------------
    private static void Emit(GoSlogLogger l, long level, GoString msg, GoSlice args)
    {
        var h = l.Handler;
        if (level < h.Level) return;
        var attrs = new List<GoSlogGAttr>(h.Preset);
        CollectArgs(attrs, args, h.Groups); // record args take the handler's current group path
        string line = h.Json ? RenderJson(level, msg.ToDotNetString(), attrs)
                             : RenderText(level, msg.ToDotNetString(), attrs);
        Fmt.WriteTo(h.Writer, line + "\n");
    }

    private static void CollectArgs(List<GoSlogGAttr> dst, GoSlice args, List<string> groups)
    {
        for (int i = 0; i < args.Len; i++)
        {
            var a = args.Data![args.Off + i];
            if (a is GoSlogAttr sa)
            {
                // slog.Group: expand the children under the group key (nested path).
                if (sa.Value is List<GoSlogGAttr> grp)
                {
                    foreach (var ch in grp)
                    {
                        var ng = new List<string>(groups) { sa.Key };
                        ng.AddRange(ch.Groups);
                        dst.Add(new GoSlogGAttr { Groups = ng, Key = ch.Key, Value = ch.Value });
                    }
                    continue;
                }
                dst.Add(new GoSlogGAttr { Groups = groups, Key = sa.Key, Value = sa.Value });
                continue;
            }
            // alternating key, value
            string key = a is GoString gs ? gs.ToDotNetString() : Str(a);
            object? val = i + 1 < args.Len ? args.Data![args.Off + ++i] : GoString.FromDotNetString("!BADKEY");
            dst.Add(new GoSlogGAttr { Groups = groups, Key = key, Value = val });
        }
    }

    public static void Logger_Debug(object l, GoString msg, GoSlice args) => Emit((GoSlogLogger)l, -4, msg, args);
    public static void Logger_Info(object l, GoString msg, GoSlice args) => Emit((GoSlogLogger)l, 0, msg, args);
    public static void Logger_Warn(object l, GoString msg, GoSlice args) => Emit((GoSlogLogger)l, 4, msg, args);
    public static void Logger_Error(object l, GoString msg, GoSlice args) => Emit((GoSlogLogger)l, 8, msg, args);

    public static object Logger_With(object l, GoSlice args)
    {
        var src = (GoSlogLogger)l;
        var h = Clone(src.Handler);
        CollectArgs(h.Preset, args, h.Groups); // attrs join the currently-open group path
        return new GoSlogLogger { Handler = h };
    }

    // (*Logger).WithGroup(name): subsequent attrs (and record args) nest under name. An empty
    // name is a no-op, as in Go.
    public static object Logger_WithGroup(object l, GoString name)
    {
        var src = (GoSlogLogger)l;
        string g = name.ToDotNetString();
        if (g.Length == 0) return src;
        var h = Clone(src.Handler);
        h.Groups.Add(g);
        return new GoSlogLogger { Handler = h };
    }

    private static GoSlogHandler Clone(GoSlogHandler src)
    {
        var h = new GoSlogHandler { Writer = src.Writer, Json = src.Json, Level = src.Level };
        h.Preset.AddRange(src.Preset);
        h.Groups.AddRange(src.Groups);
        return h;
    }

    // package-level helpers route to the default logger
    public static void Debug(GoString msg, GoSlice args) => Emit(Default(), -4, msg, args);
    public static void Info(GoString msg, GoSlice args) => Emit(Default(), 0, msg, args);
    public static void Warn(GoString msg, GoSlice args) => Emit(Default(), 4, msg, args);
    public static void Error(GoString msg, GoSlice args) => Emit(Default(), 8, msg, args);

    // --- formatting ----------------------------------------------------------
    private static string RenderText(long level, string msg, List<GoSlogGAttr> attrs)
    {
        var sb = new StringBuilder();
        sb.Append("level=").Append(LevelName(level));
        sb.Append(" msg=").Append(TextVal(GoString.FromDotNetString(msg)));
        foreach (var a in attrs)
        {
            // grouped keys are dotted: WithGroup("req") -> "req.method=…"
            string key = a.Groups.Count > 0 ? string.Join(".", a.Groups) + "." + a.Key : a.Key;
            sb.Append(' ').Append(QuoteIfNeeded(key)).Append('=').Append(TextVal(a.Value));
        }
        return sb.ToString();
    }

    private static string RenderJson(long level, string msg, List<GoSlogGAttr> attrs)
    {
        var sb = new StringBuilder();
        sb.Append("{\"level\":\"").Append(LevelName(level)).Append("\",\"msg\":");
        JsonStr(sb, msg);
        // attrs carry a group path; nest each under its groups (a non-empty group becomes an
        // object). Group paths only deepen across a logger chain, so track the open path and
        // open/close object braces as the path changes.
        var open = new List<string>();
        bool needComma = true;
        foreach (var a in attrs)
        {
            int common = 0;
            while (common < open.Count && common < a.Groups.Count && open[common] == a.Groups[common]) common++;
            for (int d = open.Count; d > common; d--) { sb.Append('}'); needComma = true; }
            if (open.Count > common) open.RemoveRange(common, open.Count - common);
            for (int d = common; d < a.Groups.Count; d++)
            {
                if (needComma) sb.Append(',');
                JsonStr(sb, a.Groups[d]);
                sb.Append(":{");
                needComma = false;
                open.Add(a.Groups[d]);
            }
            if (needComma) sb.Append(',');
            JsonStr(sb, a.Key);
            sb.Append(':');
            JsonVal(sb, a.Value);
            needComma = true;
        }
        for (int d = open.Count; d > 0; d--) sb.Append('}');
        sb.Append('}');
        return sb.ToString();
    }

    private static string TextVal(object? v) => v switch
    {
        GoString s => QuoteIfNeeded(s.ToDotNetString()),
        bool b => b ? "true" : "false",
        long n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ulong n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double d => GoCLR.Runtime.GoFtoa.Shortest(d),
        null => "<nil>",
        _ => QuoteIfNeeded(Str(v)),
    };

    private static void JsonVal(StringBuilder sb, object? v)
    {
        switch (v)
        {
            case GoString s: JsonStr(sb, s.ToDotNetString()); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case long n: sb.Append(n.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case ulong n: sb.Append(n.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(GoCLR.Runtime.GoFtoa.Shortest(d)); break;
            case null: sb.Append("null"); break;
            default: JsonStr(sb, Str(v)); break;
        }
    }

    private static string Str(object? v) => v is GoString s ? s.ToDotNetString() : (v?.ToString() ?? "");

    // slog quotes a key/value only when empty or containing a space, '=', '"' or a
    // control character (mirroring its needsQuoting).
    private static string QuoteIfNeeded(string s)
    {
        bool need = s.Length == 0;
        foreach (char c in s)
            if (c == ' ' || c == '=' || c == '"' || c < 0x20) { need = true; break; }
        if (!need) return s;
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\t') sb.Append("\\t");
            else if (c == '\r') sb.Append("\\r");
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    private static void JsonStr(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\t') sb.Append("\\t");
            else if (c == '\r') sb.Append("\\r");
            else sb.Append(c);
        }
        sb.Append('"');
    }
}
