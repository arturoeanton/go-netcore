namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the package-level functions of Go's <c>log</c> (default logger
/// to stderr). Timestamps (the default flags) are non-deterministic; SetFlags(0)
/// for reproducible output.</summary>
public static class Log
{
    private static int _flags = 3; // LstdFlags = Ldate|Ltime
    private static string _prefix = "";

    public static void SetFlags(long f) => _flags = (int)f;
    public static void SetPrefix(GoString p) => _prefix = p.ToDotNetString();
    public static long Flags() => _flags;
    public static GoString Prefix() => GoString.FromDotNetString(_prefix);

    private static void Emit(string msg)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_prefix);
        if ((_flags & 1) != 0 || (_flags & 2) != 0)
        {
            var now = System.DateTime.Now;
            if ((_flags & 1) != 0) sb.Append(now.ToString("yyyy/MM/dd "));
            if ((_flags & 2) != 0) sb.Append(now.ToString("HH:mm:ss "));
        }
        sb.Append(msg);
        if (!msg.EndsWith("\n")) sb.Append('\n');
        System.Console.Error.Write(sb.ToString());
        System.Console.Error.Flush();
    }

    public static void Print(GoSlice a) => Emit(Fmt.Sprint(a).ToDotNetString());
    public static void Println(GoSlice a) { var s = Fmt.Sprintln(a).ToDotNetString(); Emit(s.TrimEnd('\n')); }
    public static void Printf(GoString f, GoSlice a) => Emit(Fmt.Sprintf(f, a).ToDotNetString());
    public static void Fatal(GoSlice a) { Emit(Fmt.Sprint(a).ToDotNetString()); System.Environment.Exit(1); }
    public static void Fatalf(GoString f, GoSlice a) { Emit(Fmt.Sprintf(f, a).ToDotNetString()); System.Environment.Exit(1); }
    public static void Fatalln(GoSlice a) { var s = Fmt.Sprintln(a).ToDotNetString(); Emit(s.TrimEnd('\n')); System.Environment.Exit(1); }
    public static void Panic(GoSlice a) { string s = Fmt.Sprint(a).ToDotNetString(); Emit(s); throw new GoPanicException(GoString.FromDotNetString(s)); }
    public static void Panicf(GoString f, GoSlice a) { string s = Fmt.Sprintf(f, a).ToDotNetString(); Emit(s); throw new GoPanicException(GoString.FromDotNetString(s)); }
}
