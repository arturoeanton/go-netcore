namespace GoCLR.Runtime;

/// <summary>
/// GoNamed gives a boxed value the runtime *named-type identity* that the CLR
/// representation alone erases. A Go named type whose underlying type is not a
/// struct (e.g. <c>type Money int64</c>, <c>type StringSlice []string</c>) boxes
/// to the same CLR object as its underlying type — so two different named types,
/// and a named type vs its underlying, become indistinguishable. That breaks
/// interface dispatch (which named slice implements <c>sort.Interface</c>?),
/// <c>fmt</c> Stringer dispatch, and <c>%T</c>.
///
/// When such a value is boxed into an interface, lowering wraps it in a GoNamed
/// carrying a compiler-assigned type id. Consumers (interface dispatch, type
/// assertion/switch, ==, fmt, reflect) recover the identity from the wrapper.
/// Structs are NOT wrapped: their CLR type already provides identity.
/// </summary>
public sealed class GoNamed
{
    /// <summary>Compiler-assigned id of the Go named type (stable within a build).</summary>
    public long TypeId;

    /// <summary>The boxed underlying value (Int64, GoString, GoSlice, …).</summary>
    public object? Value;

    public GoNamed(long typeId, object? value)
    {
        TypeId = typeId;
        Value = value;
    }
}
