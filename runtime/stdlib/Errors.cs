namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>errors</c> package. Errors are runtime GoError
/// values, recognized by interface dispatch as satisfying the error interface.</summary>
public static class Errors
{
    public static object New(GoString text) => new GoError(text);

    public static object? Unwrap(object? err) => err is GoError g ? g.Wrapped : null;

    /// <summary>errors.Is: walks the Unwrap chain comparing identity.</summary>
    public static bool Is(object? err, object? target)
    {
        if (target == null) return err == null;
        while (err != null)
        {
            if (ReferenceEquals(err, target)) return true;
            err = err is GoError g ? g.Wrapped : null;
        }
        return false;
    }
}
