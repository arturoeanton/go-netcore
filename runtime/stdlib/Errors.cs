namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>errors</c> package. Errors are runtime GoError
/// values, recognized by interface dispatch as satisfying the error interface.</summary>
public static class Errors
{
    public static object New(GoString text) => new GoError(text);

    // errors.ErrUnsupported: the sentinel for "not supported by this implementation",
    // returned/wrapped by stdlib operations and tested with errors.Is.
    public static readonly GoError ErrUnsupportedSentinel = new(GoString.FromDotNetString("unsupported operation"));
    public static object ErrUnsupported() => ErrUnsupportedSentinel;

    /// <summary>The error produced by errors.Join: holds the joined errors and reports
    /// their messages newline-separated. It exposes the list (Go's Unwrap() []error) so
    /// errors.Is/As can descend into every joined error.</summary>
    public sealed class JoinError : IGoError
    {
        public readonly object?[] Errs;
        public JoinError(object?[] errs) { Errs = errs; }
        public GoString Error()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Errs.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(ErrString(Errs[i]));
            }
            return GoString.FromDotNetString(sb.ToString());
        }
    }

    static string ErrString(object? e)
    {
        if (e is IGoError g) return g.Error().ToDotNetString();
        if (e != null && Bridge.HasMethod(e, "Error") && Bridge.CallMethod(e, "Error") is GoString gs) return gs.ToDotNetString();
        return "";
    }

    /// <summary>errors.Join: returns an error wrapping the non-nil arguments, or nil when
    /// every argument is nil (matching Go, which also returns nil for an empty call).</summary>
    public static object? Join(GoSlice errs)
    {
        var kept = new System.Collections.Generic.List<object?>();
        for (int i = 0; i < errs.Len; i++)
        {
            var e = errs.Data![errs.Off + i];
            if (e != null) kept.Add(e);
        }
        if (kept.Count == 0) return null;
        return new JoinError(kept.ToArray());
    }

    /// <summary>errors.Unwrap: the GoError (%w) wrapped error, or a user error's own
    /// Unwrap() method via the callback bridge.</summary>
    public static object? Unwrap(object? err)
    {
        if (err is GoError g) return g.Multi != null ? null : g.Wrapped; // wrapErrors has Unwrap() []error, not error
        if (err is GoCLR.Runtime.IGoWrapped w) return w.GoUnwrapped();
        if (err != null && Bridge.HasMethod(err, "Unwrap")) return Bridge.CallMethod(err, "Unwrap");
        return null;
    }

    /// <summary>errors.Is: walks the Unwrap chain comparing identity, and consults each
    /// error's own Is(target) bool method, matching Go.</summary>
    public static bool Is(object? err, object? target)
    {
        if (target == null) return err == null;
        while (err != null)
        {
            if (ReferenceEquals(err, target)) return true;
            if (err is IGoErrorIs eis && eis.GoIs(target)) return true; // shim error's own Is(target)
            if (Bridge.HasMethod(err, "Is") && Bridge.CallMethod(err, "Is", target) is bool b && b) return true;
            if (err is JoinError je) // Unwrap() []error: any joined error matching is a match
            {
                foreach (var e in je.Errs) if (Is(e, target)) return true;
                return false;
            }
            if (err is GoError gm && gm.Multi != null) // fmt.Errorf multi-%w: Unwrap() []error
            {
                foreach (var e in gm.Multi) if (Is(e, target)) return true;
                return false;
            }
            err = Unwrap(err);
        }
        return false;
    }

    /// <summary>errors.As: walks the Unwrap chain for the first error whose concrete
    /// type matches the target's element type (by CLR type name passed from the
    /// compiler), assigning it to *target and returning true. Follows both the GoError
    /// (%w) chain and a user error's own Unwrap().</summary>
    public static bool As(object? err, object? target, GoString typeName)
    {
        if (target is not GoPtr tp) return false;
        string want = typeName.ToDotNetString();
        while (err != null)
        {
            // A pointer-receiver error is a GoPtr around the concrete struct. Match the target
            // type by the goclr-generated struct's CLR name or, for a shim error struct
            // (os.PathError, csv.ParseError, …), by its Go type name via the [GoShim] registry
            // (`want` carries the Go name then).
            object? concrete = err is GoPtr gp ? gp.Value : err;
            if (want.Length != 0 && concrete != null &&
                (concrete.GetType().Name == want || ShimTypes.IsStrict(concrete, want)))
            {
                tp.Value = err;
                return true;
            }
            if (err is JoinError je) // Unwrap() []error: search each joined error
            {
                foreach (var e in je.Errs) if (As(e, target, typeName)) return true;
                return false;
            }
            if (err is GoError gm && gm.Multi != null) // fmt.Errorf multi-%w: Unwrap() []error
            {
                foreach (var e in gm.Multi) if (As(e, target, typeName)) return true;
                return false;
            }
            err = Unwrap(err);
        }
        return false;
    }
}
