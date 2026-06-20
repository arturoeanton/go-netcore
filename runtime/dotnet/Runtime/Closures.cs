namespace GoCLR.Runtime;

/// <summary>
/// GoClosure is a Go function value. The backend lambda-lifts each function
/// literal to a static method and stores its identity (<see cref="Id"/>) plus the
/// captured cells (<see cref="Env"/>, an array of GoPtr). A generated dispatcher
/// switches on Id to invoke the right lifted method. Capturing by GoPtr cell
/// gives Go's by-reference capture semantics.
/// </summary>
public sealed class GoClosure
{
    public int Id;
    public object?[] Env = System.Array.Empty<object?>();

    /// <summary>For function values produced by the runtime/stdlib (e.g. a
    /// context.CancelFunc) rather than lambda-lifted Go code: a native delegate the
    /// dispatcher falls back to when no lifted Id matches. Id is unused (-1).</summary>
    public System.Func<object?[], object?>? Native;
}

/// <summary>Closure construction/accessors the compiler calls into.</summary>
public static class GoClosures
{
    public static GoClosure New(long id, object?[] env) => new() { Id = (int)id, Env = env };
    public static long Id(GoClosure f) => f.Id;
    public static object?[] Env(GoClosure f) => f.Env;
}
