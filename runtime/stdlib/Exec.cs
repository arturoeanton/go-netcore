namespace GoCLR.Stdlib;

using System.Diagnostics;
using GoCLR.Runtime;

/// <summary>An *exec.Cmd handle.</summary>
public sealed class GoCmd { public string Name = ""; public System.Collections.Generic.List<string> Args = new(); }

/// <summary>Shim for a subset of Go's <c>os/exec</c> (subprocess execution).</summary>
public static class Exec
{
    public static object Command(GoString name, GoSlice args)
    {
        var c = new GoCmd { Name = name.ToDotNetString() };
        for (int i = 0; i < args.Len; i++) c.Args.Add(((GoString)args.Data![args.Off + i]!).ToDotNetString());
        return c;
    }

    private static GoSlice Bytes(string s)
    {
        var b = System.Text.Encoding.UTF8.GetBytes(s);
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }

    private static (string outp, string err, int code) RunProc(GoCmd c, bool combined)
    {
        var psi = new ProcessStartInfo { FileName = c.Name, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in c.Args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd();
            string e = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (combined ? o + e : o, e, p.ExitCode);
        }
        catch (System.Exception ex) { return ("", ex.Message, -1); }
    }

    public static object?[] Cmd_Output(object cmd)
    {
        var (o, _, code) = RunProc((GoCmd)cmd, false);
        object? err = code == 0 ? null : new GoError(GoString.FromDotNetString("exit status " + code));
        return new object?[] { Bytes(o), err };
    }
    public static object?[] Cmd_CombinedOutput(object cmd)
    {
        var (o, _, code) = RunProc((GoCmd)cmd, true);
        object? err = code == 0 ? null : new GoError(GoString.FromDotNetString("exit status " + code));
        return new object?[] { Bytes(o), err };
    }
    public static object? Cmd_Run(object cmd)
    {
        var (_, _, code) = RunProc((GoCmd)cmd, false);
        return code == 0 ? null : new GoError(GoString.FromDotNetString("exit status " + code));
    }
}
