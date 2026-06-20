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

    /// <summary>errors.As: walks the Unwrap chain for the first error whose concrete
    /// type matches the target's element type (by CLR type name passed from the
    /// compiler), assigning it to *target and returning true.</summary>
    public static bool As(object? err, object? target, GoString typeName)
    {
        if (target is not GoPtr tp) return false;
        string want = typeName.ToDotNetString();
        while (err != null)
        {
            // A pointer-receiver error is a GoPtr around the concrete struct.
            object? concrete = err is GoPtr gp ? gp.Value : err;
            if (want.Length != 0 && concrete != null && concrete.GetType().Name == want)
            {
                tp.Value = err;
                return true;
            }
            err = err is GoError g ? g.Wrapped : null;
        }
        return false;
    }
}
