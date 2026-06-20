namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the one go/ast helper goja uses: IsExported reports whether
/// a name begins with an upper-case Unicode letter (Go's exported-identifier rule).</summary>
public static class Ast
{
    public static bool IsExported(GoString name)
    {
        string s = name.ToDotNetString();
        if (s.Length == 0) return false;
        var first = System.Text.Rune.GetRuneAt(s, 0);
        return System.Text.Rune.IsUpper(first);
    }
}
