namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Bridges runtime/stdlib-produced function values (native delegates) to
/// the compiler's GoClosure dispatcher. The generated __invoke method falls back
/// here when no lambda-lifted closure Id matches.</summary>
public static class NativeClosures
{
    public static object? InvokeNative(GoClosure f, object?[] args)
        => f.Native != null ? f.Native(args) : null;

    /// <summary>Wrap a C# delegate as a Go function value.</summary>
    public static GoClosure Make(System.Func<object?[], object?> fn)
        => new GoClosure { Id = -1, Native = fn };

    private static readonly System.Collections.Generic.Dictionary<string, System.Reflection.MethodInfo> Cache = new();

    /// <summary>Wrap a shimmed stdlib function (e.g. unicode.IsSpace, sha256.New) as
    /// a Go function value, invoked by reflection with the closure's args.</summary>
    public static GoClosure FromShim(GoString type, GoString method)
    {
        string key = type.ToDotNetString() + "." + method.ToDotNetString();
        if (!Cache.TryGetValue(key, out var mi))
        {
            var t = System.Type.GetType("GoCLR.Stdlib." + type.ToDotNetString())!;
            mi = t.GetMethod(method.ToDotNetString())!;
            Cache[key] = mi;
        }
        return new GoClosure { Id = -1, Native = args => InvokeShim(mi, args) };
    }

    private static object? InvokeShim(System.Reflection.MethodInfo mi, object?[] args)
    {
        var ps = mi.GetParameters();
        var conv = new object?[ps.Length];
        // A variadic shim (e.g. fmt.Sprintf(format, args ...any)) lowers its trailing
        // parameter to a GoSlice. Called through a function value, the trailing arguments
        // arrive unpacked, so pack them here — unless the caller already passed the slice
        // (f(args...) / a non-variadic []T parameter), in which case the last arg is a GoSlice.
        bool lastIsSlice = ps.Length > 0 && ps[ps.Length - 1].ParameterType == typeof(GoSlice);
        if (lastIsSlice && (args.Length != ps.Length || !(args[ps.Length - 1] is GoSlice)))
        {
            int nFixed = ps.Length - 1;
            for (int i = 0; i < nFixed; i++)
                conv[i] = Coerce(i < args.Length ? args[i] : null, ps[i].ParameterType);
            int extra = args.Length - nFixed;
            if (extra < 0) extra = 0;
            var data = new object?[extra];
            for (int i = 0; i < extra; i++) data[i] = args[nFixed + i];
            conv[nFixed] = new GoSlice { Data = data, Off = 0, Len = extra, Cap = extra };
            return mi.Invoke(null, conv);
        }
        for (int i = 0; i < ps.Length; i++)
            conv[i] = Coerce(i < args.Length ? args[i] : null, ps[i].ParameterType);
        return mi.Invoke(null, conv);
    }

    private static object? Coerce(object? v, System.Type target)
    {
        if (v == null || target.IsInstanceOfType(v)) return v;
        if (target == typeof(int) || target == typeof(long) || target == typeof(uint) || target == typeof(ulong)
            || target == typeof(short) || target == typeof(ushort) || target == typeof(byte) || target == typeof(sbyte)
            || target == typeof(double) || target == typeof(float))
            return System.Convert.ChangeType(v, target);
        return v;
    }
}
