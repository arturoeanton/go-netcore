namespace GoCLR.Stdlib;

using System.Collections.Generic;
using GoCLR.Runtime;

/// <summary>A single flag.Flag: its name, usage, kind and the pointer cell holding the value.</summary>
public sealed class GoFlag { public string Name = "", Usage = "", DefValue = ""; public GoPtr Ptr = new(); public int Kind; }

/// <summary>A flag.FlagSet. Kind: 0=bool 1=int 2=int64 3=uint 4=uint64 5=float64 6=string 7=duration.</summary>
[GoShim("flag.FlagSet")]
public sealed class GoFlagSet
{
    public string Name = "";
    public int ErrorHandling;                 // 0=ContinueOnError 1=ExitOnError 2=PanicOnError
    public readonly Dictionary<string, GoFlag> Formal = new();
    public readonly Dictionary<string, GoFlag> Actual = new();
    public List<string> Args = new();
    public bool Parsed;
}

/// <summary>Shim for flag: define typed flags, parse argv. Parsing is ported from
/// src/flag/flag.go (parseOne) so behaviour and error strings match go run. PrintDefaults /
/// usage output, Visit/Lookup (*Flag access) and Func/TextVar are not yet provided.</summary>
public static class Flag
{
    // The package-level default set (flag.CommandLine), ExitOnError like Go.
    private static readonly GoFlagSet _cmd = new() { Name = "", ErrorHandling = 1 };
    public static object CommandLine() => _cmd;
    public static object ErrHelpVar() => ErrHelpSentinel;
    private static readonly GoError ErrHelpSentinel = new(GoString.FromDotNetString("flag: help requested"));

    // flag.Lookup(name) *flag.Flag: nil — *flag.Flag field access is not yet modelled, so
    // Lookup is reported as absent (kept for libraries that only probe for testing flags).
    public static GoPtr? Lookup(GoString name) => null;

    private static GoFlagSet FS(object o) => (GoFlagSet)o;

    public static object NewFlagSet(GoString name, long errorHandling) =>
        new GoFlagSet { Name = name.ToDotNetString(), ErrorHandling = (int)errorHandling };
    public static object NewFlagSetZero() => new GoFlagSet();

    // ---- value formatting (Value.String of the default) ----------------------------------
    private static string DefStr(int kind, object? v) => kind switch
    {
        0 => (bool)v! ? "true" : "false",
        5 => Strconv.FormatFloat((double)v!, (int)'g', -1, 64).ToDotNetString(),
        6 => ((GoString)v!).ToDotNetString(),
        7 => Time.Duration_String((long)v!).ToDotNetString(),
        3 or 4 => ((ulong)v!).ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => System.Convert.ToInt64(v).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static void Define(GoFlagSet fs, GoPtr ptr, string name, string usage, int kind, object? def)
    {
        if (name.StartsWith("-")) throw new GoPanicException(GoString.FromDotNetString($"flag \"{name}\" begins with -"));
        if (name.Contains("=")) throw new GoPanicException(GoString.FromDotNetString($"flag \"{name}\" contains ="));
        if (fs.Formal.ContainsKey(name))
            throw new GoPanicException(GoString.FromDotNetString(fs.Name == "" ? $"flag redefined: {name}" : $"{fs.Name} flag redefined: {name}"));
        fs.Formal[name] = new GoFlag { Name = name, Usage = usage, Kind = kind, Ptr = ptr, DefValue = DefStr(kind, def) };
    }

    // ---- value parsing (Value.Set): returns error string or null; writes the cell ---------
    private static string? SetValue(GoFlag fl, string s)
    {
        switch (fl.Kind)
        {
            case 0: { var r = Strconv.ParseBool(GoString.FromDotNetString(s)); if (r[1] != null) return "parse error"; fl.Ptr.Value = (bool)r[0]!; return null; }
            case 1: case 2: { var r = Strconv.ParseInt(GoString.FromDotNetString(s), 0, 64); if (r[1] != null) return NumErr("ParseInt", s, r[1]); fl.Ptr.Value = (long)r[0]!; return null; }
            case 3: case 4: { var r = Strconv.ParseUint(GoString.FromDotNetString(s), 0, 64); if (r[1] != null) return NumErr("ParseUint", s, r[1]); fl.Ptr.Value = (ulong)r[0]!; return null; }
            case 5: { var r = Strconv.ParseFloat(GoString.FromDotNetString(s), 64); if (r[1] != null) return NumErr("ParseFloat", s, r[1]); fl.Ptr.Value = (double)r[0]!; return null; }
            case 6: fl.Ptr.Value = GoString.FromDotNetString(s); return null;
            case 7: { var r = Time.ParseDuration(GoString.FromDotNetString(s)); if (r[1] != null) return "parse error"; fl.Ptr.Value = (long)r[0]!; return null; }
        }
        return null;
    }
    // The numeric value types map strconv's *NumError to flag's errParse / errRange (numError),
    // so Set surfaces just "parse error" or "value out of range".
    private static string NumErr(string fn, string s, object? strErr) =>
        strErr is GoError g && g.Error().ToDotNetString().Contains("out of range") ? "value out of range" : "parse error";

    // ---- typed definers (FlagSet receiver) -----------------------------------------------
    public static GoPtr FS_Bool(object fs, GoString n, bool d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 0, d); return p; }
    public static GoPtr FS_Int(object fs, GoString n, long d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 1, d); return p; }
    public static GoPtr FS_Int64(object fs, GoString n, long d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 2, d); return p; }
    public static GoPtr FS_Uint(object fs, GoString n, ulong d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 3, d); return p; }
    public static GoPtr FS_Uint64(object fs, GoString n, ulong d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 4, d); return p; }
    public static GoPtr FS_Float64(object fs, GoString n, double d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 5, d); return p; }
    public static GoPtr FS_String(object fs, GoString n, GoString d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 6, d); return p; }
    public static GoPtr FS_Duration(object fs, GoString n, long d, GoString u) { var p = new GoPtr { Value = d }; Define(FS(fs), p, n.ToDotNetString(), u.ToDotNetString(), 7, d); return p; }

    // Var forms: the cell is the caller's pointer; seed it with the default.
    public static void FS_BoolVar(object fs, GoPtr p, GoString n, bool d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 0, d); }
    public static void FS_IntVar(object fs, GoPtr p, GoString n, long d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 1, d); }
    public static void FS_Int64Var(object fs, GoPtr p, GoString n, long d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 2, d); }
    public static void FS_UintVar(object fs, GoPtr p, GoString n, ulong d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 3, d); }
    public static void FS_Uint64Var(object fs, GoPtr p, GoString n, ulong d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 4, d); }
    public static void FS_Float64Var(object fs, GoPtr p, GoString n, double d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 5, d); }
    public static void FS_StringVar(object fs, GoPtr p, GoString n, GoString d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 6, d); }
    public static void FS_DurationVar(object fs, GoPtr p, GoString n, long d, GoString u) { var g = (GoPtr)p; g.Value = d; Define(FS(fs), g, n.ToDotNetString(), u.ToDotNetString(), 7, d); }

    // ---- parse + accessors ---------------------------------------------------------------
    public static object? FS_Set(object fs, GoString name, GoString value)
    {
        var f = FS(fs); string n = name.ToDotNetString();
        if (!f.Formal.TryGetValue(n, out var fl)) return new GoError(GoString.FromDotNetString($"no such flag -{n}"));
        var e = SetValue(fl, value.ToDotNetString());
        if (e != null) return new GoError(GoString.FromDotNetString(e));
        f.Actual[n] = fl;
        return null;
    }

    public static object? FS_Parse(object fs, GoSlice arguments)
    {
        var f = FS(fs);
        f.Parsed = true;
        f.Args = new List<string>();
        for (int i = 0; i < arguments.Len; i++) f.Args.Add(((GoString)arguments.Data![arguments.Off + i]!).ToDotNetString());
        while (true)
        {
            var (seen, err) = ParseOne(f);
            if (seen) continue;
            if (err == null) break;
            if (f.ErrorHandling == 0) return err;          // ContinueOnError
            throw new GoPanicException(((GoError)err).Error()); // Exit/PanicOnError surface the error
        }
        return null;
    }

    private static (bool, object?) ParseOne(GoFlagSet f)
    {
        if (f.Args.Count == 0) return (false, null);
        string s = f.Args[0];
        if (s.Length < 2 || s[0] != '-') return (false, null);
        int numMinuses = 1;
        if (s[1] == '-') { numMinuses++; if (s.Length == 2) { f.Args.RemoveAt(0); return (false, null); } }
        string name = s.Substring(numMinuses);
        if (name.Length == 0 || name[0] == '-' || name[0] == '=') return (false, Err($"bad flag syntax: {s}"));
        f.Args.RemoveAt(0);
        bool hasValue = false; string value = "";
        for (int i = 1; i < name.Length; i++) if (name[i] == '=') { value = name.Substring(i + 1); hasValue = true; name = name.Substring(0, i); break; }
        if (!f.Formal.TryGetValue(name, out var fl))
        {
            if (name == "help" || name == "h") return (false, ErrHelpSentinel);
            return (false, Err($"flag provided but not defined: -{name}"));
        }
        if (fl.Kind == 0) // bool: doesn't need an arg
        {
            var e = SetValue(fl, hasValue ? value : "true");
            if (e != null) return (false, Err(hasValue ? $"invalid boolean value \"{value}\" for -{name}: {e}" : $"invalid boolean flag {name}: {e}"));
        }
        else
        {
            if (!hasValue && f.Args.Count > 0) { hasValue = true; value = f.Args[0]; f.Args.RemoveAt(0); }
            if (!hasValue) return (false, Err($"flag needs an argument: -{name}"));
            var e = SetValue(fl, value);
            if (e != null) return (false, Err($"invalid value \"{value}\" for flag -{name}: {e}"));
        }
        f.Actual[name] = fl;
        return (true, null);
    }
    private static object Err(string msg) => new GoError(GoString.FromDotNetString(msg));

    public static bool FS_Parsed(object fs) => FS(fs).Parsed;
    public static GoString FS_Name(object fs) => GoString.FromDotNetString(FS(fs).Name);
    public static long FS_NArg(object fs) => FS(fs).Args.Count;
    public static long FS_NFlag(object fs) => FS(fs).Actual.Count;
    public static GoString FS_Arg(object fs, long i) { var a = FS(fs).Args; return GoString.FromDotNetString(i < 0 || i >= a.Count ? "" : a[(int)i]); }
    public static GoSlice FS_Args(object fs)
    {
        var a = FS(fs).Args; var d = new object?[a.Count];
        for (int i = 0; i < a.Count; i++) d[i] = GoString.FromDotNetString(a[i]);
        return new GoSlice { Data = d, Off = 0, Len = a.Count, Cap = a.Count };
    }
    public static long FS_ErrorHandling(object fs) => FS(fs).ErrorHandling;

    // ---- package-level wrappers (operate on CommandLine) ---------------------------------
    public static GoPtr Bool(GoString n, bool d, GoString u) => FS_Bool(_cmd, n, d, u);
    public static GoPtr Int(GoString n, long d, GoString u) => FS_Int(_cmd, n, d, u);
    public static GoPtr Int64(GoString n, long d, GoString u) => FS_Int64(_cmd, n, d, u);
    public static GoPtr Uint(GoString n, ulong d, GoString u) => FS_Uint(_cmd, n, d, u);
    public static GoPtr Uint64(GoString n, ulong d, GoString u) => FS_Uint64(_cmd, n, d, u);
    public static GoPtr Float64(GoString n, double d, GoString u) => FS_Float64(_cmd, n, d, u);
    public static GoPtr String(GoString n, GoString d, GoString u) => FS_String(_cmd, n, d, u);
    public static GoPtr Duration(GoString n, long d, GoString u) => FS_Duration(_cmd, n, d, u);
    public static void BoolVar(GoPtr p, GoString n, bool d, GoString u) => FS_BoolVar(_cmd, p, n, d, u);
    public static void IntVar(GoPtr p, GoString n, long d, GoString u) => FS_IntVar(_cmd, p, n, d, u);
    public static void Int64Var(GoPtr p, GoString n, long d, GoString u) => FS_Int64Var(_cmd, p, n, d, u);
    public static void UintVar(GoPtr p, GoString n, ulong d, GoString u) => FS_UintVar(_cmd, p, n, d, u);
    public static void Uint64Var(GoPtr p, GoString n, ulong d, GoString u) => FS_Uint64Var(_cmd, p, n, d, u);
    public static void Float64Var(GoPtr p, GoString n, double d, GoString u) => FS_Float64Var(_cmd, p, n, d, u);
    public static void StringVar(GoPtr p, GoString n, GoString d, GoString u) => FS_StringVar(_cmd, p, n, d, u);
    public static void DurationVar(GoPtr p, GoString n, long d, GoString u) => FS_DurationVar(_cmd, p, n, d, u);
    public static object? Set(GoString n, GoString v) => FS_Set(_cmd, n, v);
    public static bool Parsed() => _cmd.Parsed;
    public static long NArg() => _cmd.Args.Count;
    public static long NFlag() => _cmd.Actual.Count;
    public static GoString Arg(long i) => FS_Arg(_cmd, i);
    public static GoSlice Args() => FS_Args(_cmd);
}
