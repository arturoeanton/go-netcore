namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the package-level functions of Go's <c>log</c> (default logger
/// to stderr). Timestamps (the default flags) are non-deterministic; SetFlags(0)
/// for reproducible output.</summary>
/// <summary>A *log.Logger writing to its output (default stderr).</summary>
public sealed class GoLogger { public object? Out; public string Prefix = ""; public int Flags; }

public static class Log
{
    // The package-level functions delegate to this std logger (Go: var std = New(os.Stderr, ...)).
    // Out == null means stderr. Default flags are LstdFlags (Ldate|Ltime).
    private static readonly GoLogger _std = new() { Out = null, Prefix = "", Flags = 3 };

    public static void SetFlags(long f) => _std.Flags = (int)f;
    public static void SetPrefix(GoString p) => _std.Prefix = p.ToDotNetString();
    public static long Flags() => _std.Flags;
    public static GoString Prefix() => GoString.FromDotNetString(_std.Prefix);
    // log.Default() *Logger / log.SetOutput / log.Writer / log.Output operate on std.
    public static object Default() => _std;
    public static void SetOutput(object? w) => _std.Out = w;
    public static object? Writer() => _std.Out;
    public static object? Output(long calldepth, GoString s) { Emit(s.ToDotNetString()); return null; }

    private static void Emit(string msg)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_std.Prefix);
        if ((_std.Flags & 1) != 0 || (_std.Flags & 2) != 0)
        {
            var now = System.DateTime.Now;
            if ((_std.Flags & 1) != 0) sb.Append(now.ToString("yyyy/MM/dd "));
            if ((_std.Flags & 2) != 0) sb.Append(now.ToString("HH:mm:ss "));
        }
        sb.Append(msg);
        if (!msg.EndsWith("\n")) sb.Append('\n');
        if (_std.Out != null) Fmt.WriteTo(_std.Out, sb.ToString());
        else { System.Console.Error.Write(sb.ToString()); System.Console.Error.Flush(); }
    }
    // log.Panicln: output the line, then panic with the Sprintln string (newline included, as Go does).
    public static void Panicln(GoSlice a) { string s = Fmt.Sprintln(a).ToDotNetString(); Emit(s.TrimEnd('\n')); throw new GoPanicException(GoString.FromDotNetString(s)); }

    public static void Print(GoSlice a) => Emit(Fmt.Sprint(a).ToDotNetString());
    public static void Println(GoSlice a) { var s = Fmt.Sprintln(a).ToDotNetString(); Emit(s.TrimEnd('\n')); }
    public static void Printf(GoString f, GoSlice a) => Emit(Fmt.Sprintf(f, a).ToDotNetString());
    public static void Fatal(GoSlice a) { Emit(Fmt.Sprint(a).ToDotNetString()); System.Environment.Exit(1); }
    public static void Fatalf(GoString f, GoSlice a) { Emit(Fmt.Sprintf(f, a).ToDotNetString()); System.Environment.Exit(1); }
    public static void Fatalln(GoSlice a) { var s = Fmt.Sprintln(a).ToDotNetString(); Emit(s.TrimEnd('\n')); System.Environment.Exit(1); }
    public static void Panic(GoSlice a) { string s = Fmt.Sprint(a).ToDotNetString(); Emit(s); throw new GoPanicException(GoString.FromDotNetString(s)); }
    public static void Panicf(GoString f, GoSlice a) { string s = Fmt.Sprintf(f, a).ToDotNetString(); Emit(s); throw new GoPanicException(GoString.FromDotNetString(s)); }

    // log.New(out, prefix, flag) *Logger.
    public static object New(object? outw, GoString prefix, long flag) => new GoLogger { Out = outw, Prefix = prefix.ToDotNetString(), Flags = (int)flag };
    public static object NewLoggerZero() => new GoLogger { Out = null };

    private static void LEmit(GoLogger lg, string msg)
    {
        string line = lg.Prefix + msg;
        if (lg.Out != null) Fmt.WriteTo(lg.Out, line + "\n");
        else { System.Console.Error.Write(line + "\n"); System.Console.Error.Flush(); }
    }
    public static void Logger_Print(object l, GoSlice a) => LEmit((GoLogger)l, Fmt.Sprint(a).ToDotNetString());
    public static void Logger_Println(object l, GoSlice a) => LEmit((GoLogger)l, Fmt.Sprintln(a).ToDotNetString().TrimEnd('\n'));
    public static void Logger_Printf(object l, GoString f, GoSlice a) => LEmit((GoLogger)l, Fmt.Sprintf(f, a).ToDotNetString());
    public static object? Logger_Output(object l, long calldepth, GoString s) { LEmit((GoLogger)l, s.ToDotNetString()); return null; }
    public static void Logger_Fatal(object l, GoSlice a) { LEmit((GoLogger)l, Fmt.Sprint(a).ToDotNetString()); System.Environment.Exit(1); }
    public static void Logger_Fatalf(object l, GoString f, GoSlice a) { LEmit((GoLogger)l, Fmt.Sprintf(f, a).ToDotNetString()); System.Environment.Exit(1); }
    public static void Logger_Fatalln(object l, GoSlice a) { LEmit((GoLogger)l, Fmt.Sprintln(a).ToDotNetString().TrimEnd('\n')); System.Environment.Exit(1); }
    public static void Logger_Panic(object l, GoSlice a) { var m = Fmt.Sprint(a).ToDotNetString(); LEmit((GoLogger)l, m); throw new GoPanicException(GoString.FromDotNetString(m)); }
    public static void Logger_Panicf(object l, GoString f, GoSlice a) { var m = Fmt.Sprintf(f, a).ToDotNetString(); LEmit((GoLogger)l, m); throw new GoPanicException(GoString.FromDotNetString(m)); }
    public static void Logger_Panicln(object l, GoSlice a) { var s = Fmt.Sprintln(a).ToDotNetString(); LEmit((GoLogger)l, s.TrimEnd('\n')); throw new GoPanicException(GoString.FromDotNetString(s)); }
    public static void Logger_SetFlags(object l, long f) => ((GoLogger)l).Flags = (int)f;
    public static long Logger_Flags(object l) => ((GoLogger)l).Flags;
    public static void Logger_SetPrefix(object l, GoString p) => ((GoLogger)l).Prefix = p.ToDotNetString();
    public static GoString Logger_Prefix(object l) => GoString.FromDotNetString(((GoLogger)l).Prefix);
    public static void Logger_SetOutput(object l, object? w) => ((GoLogger)l).Out = w;
    public static object? Logger_Writer(object l) => ((GoLogger)l).Out;
}
