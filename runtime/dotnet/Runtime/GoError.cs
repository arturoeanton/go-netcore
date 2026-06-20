namespace GoCLR.Runtime;

/// <summary>
/// IGoError is the runtime view of Go's built-in <c>error</c> interface
/// (<c>Error() string</c>). Concrete error types implemented in compiled Go code
/// satisfy this, and the overlay's errors/fmt packages produce values that do too.
/// </summary>
public interface IGoError
{
    GoString Error();
}

/// <summary>
/// GoError is the simplest concrete error, as produced by errors.New and
/// fmt.Errorf. nil error is represented by a null IGoError reference. Wrapped
/// holds the error this one wraps (errors.Unwrap), or null.
/// </summary>
public sealed class GoError : IGoError
{
    private readonly GoString _msg;
    public object? Wrapped;

    public GoError(GoString msg) { _msg = msg; }
    public GoError(GoString msg, object? wrapped) { _msg = msg; Wrapped = wrapped; }
    public GoError(string msg) { _msg = GoString.FromDotNetString(msg); }

    public GoString Error() => _msg;

    public string Message => _msg.ToDotNetString();

    public override string ToString() => Message;

    /// <summary>errors.New(text).</summary>
    public static IGoError New(GoString text) => new GoError(text);
}

/// <summary>Operations on error values used by interface dispatch.</summary>
public static class GoErrors
{
    /// <summary>The error interface's Error() method for any runtime error value.</summary>
    public static GoString Error(IGoError e) => e.Error();
}
