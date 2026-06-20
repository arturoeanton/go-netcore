namespace GoCLR.Runtime;

/// <summary>
/// GoPanicException carries a Go panic value across the .NET stack. It is thrown
/// by <c>panic(v)</c> and the runtime's bounds/nil checks, and is intercepted by
/// the try/finally scaffolding that lowering emits around functions that use
/// <c>defer</c>.
/// </summary>
public sealed class GoPanicException : Exception
{
    /// <summary>The Go value passed to panic (often a GoString or an error).</summary>
    public object? Value { get; }

    /// <summary>True once a deferred recover() has consumed this panic.</summary>
    public bool Recovered { get; internal set; }

    public GoPanicException(object? value)
        : base(DescribeValue(value))
    {
        Value = value;
        if (System.Environment.GetEnvironmentVariable("GOCLR_PANIC_TRACE") != null)
            System.Console.Error.WriteLine("[panic-trace] " + DescribeValue(value) + "\n" + System.Environment.StackTrace);
    }

    private static string DescribeValue(object? value) => value switch
    {
        null => "panic: nil",
        GoString gs => "panic: " + gs.ToDotNetString(),
        GoError ge => "panic: " + ge.Message,
        _ => "panic: " + value,
    };
}
