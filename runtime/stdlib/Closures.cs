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
}
