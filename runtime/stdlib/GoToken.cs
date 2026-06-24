namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A go/token.Position: a resolved source position (file, byte offset, 1-based
/// line and column).</summary>
[GoShim("go/token.Position")]
public sealed class GoPosition { public string Filename = ""; public long Offset, Line, Column; }

/// <summary>A go/token.File: a file in a FileSet, with its base Pos, size and line-offset
/// table (ported from src/go/token/position.go; the line-info adjustment path is omitted).</summary>
[GoShim("go/token.File")]
public sealed class GoTokenFile { public string Name = ""; public long Base; public long Size; public System.Collections.Generic.List<int> Lines = new() { 0 }; }

/// <summary>A go/token.FileSet: assigns disjoint Pos ranges to files.</summary>
[GoShim("go/token.FileSet")]
public sealed class GoTokenFileSet { public long Base = 1; public System.Collections.Generic.List<GoTokenFile> Files = new(); }

/// <summary>go/token: Token enum methods + identifier/keyword predicates, with the token
/// name table and range markers transcribed from src/go/token/token.go.</summary>
public static class GoToken
{
    // ---- FileSet -------------------------------------------------------------------------
    public static object NewFileSet() => new GoTokenFileSet();
    public static object FileSetZero() => new GoTokenFileSet();
    public static long FileSet_Base(object s) => ((GoTokenFileSet)s).Base;
    public static object FileSet_AddFile(object so, GoString filename, long baseP, long size)
    {
        var s = (GoTokenFileSet)so;
        long b = baseP < 0 ? s.Base : baseP;
        if (b < s.Base) throw new GoPanicException(GoString.FromDotNetString($"invalid base {b} (should be >= {s.Base})"));
        if (size < 0) throw new GoPanicException(GoString.FromDotNetString($"invalid size {size} (should be >= 0)"));
        var f = new GoTokenFile { Name = filename.ToDotNetString(), Base = b, Size = size };
        long nb = b + size + 1;
        if (nb < 0) throw new GoPanicException(GoString.FromDotNetString("token.Pos offset overflow (> 2G of source code in file set)"));
        s.Base = nb;
        s.Files.Add(f);
        return f;
    }
    private static GoTokenFile? FileFor(GoTokenFileSet s, long p)
    {
        foreach (var f in s.Files) if (f.Base <= p && p <= f.Base + f.Size) return f;
        return null;
    }
    public static object? FileSet_File(object so, long p) => p != 0 ? FileFor((GoTokenFileSet)so, p) : null;
    public static object FileSet_PositionFor(object so, long p, bool adjusted)
    {
        if (p != 0) { var f = FileFor((GoTokenFileSet)so, p); if (f != null) return Position(f, p); }
        return new GoPosition();
    }
    public static object FileSet_Position(object so, long p) => FileSet_PositionFor(so, p, true);

    // ---- File ----------------------------------------------------------------------------
    public static GoString File_Name(object f) => GoString.FromDotNetString(((GoTokenFile)f).Name);
    public static long File_Base(object f) => ((GoTokenFile)f).Base;
    public static long File_Size(object f) => ((GoTokenFile)f).Size;
    public static long File_LineCount(object f) => ((GoTokenFile)f).Lines.Count;
    public static void File_AddLine(object fo, long offset)
    {
        var f = (GoTokenFile)fo;
        if ((f.Lines.Count == 0 || f.Lines[^1] < offset) && offset < f.Size) f.Lines.Add((int)offset);
    }
    public static long File_Offset(object fo, long p) { var f = (GoTokenFile)fo; return FixOffset(f, p - f.Base); }
    public static long File_Pos(object fo, long offset) { var f = (GoTokenFile)fo; return f.Base + FixOffset(f, offset); }
    public static long File_Line(object fo, long p) => ((GoPosition)File_Position(fo, p)).Line;
    public static object File_Position(object fo, long p) => File_PositionFor(fo, p, true);
    public static object File_PositionFor(object fo, long p, bool adjusted)
    {
        if (p != 0) return Position((GoTokenFile)fo, p);
        return new GoPosition();
    }
    public static long File_LineStart(object fo, long line)
    {
        var f = (GoTokenFile)fo;
        if (line < 1) throw new GoPanicException(GoString.FromDotNetString($"invalid line number {line} (should be >= 1)"));
        if (line > f.Lines.Count) throw new GoPanicException(GoString.FromDotNetString($"invalid line number {line} (should be < {f.Lines.Count})"));
        return f.Base + f.Lines[(int)line - 1];
    }
    public static bool File_SetLines(object fo, GoSlice lines)
    {
        var f = (GoTokenFile)fo;
        var ls = new System.Collections.Generic.List<int>(lines.Len);
        for (int i = 0; i < lines.Len; i++)
        {
            int off = (int)System.Convert.ToInt64(lines.Data![lines.Off + i]);
            if (i > 0 && off <= ls[i - 1] || f.Size <= off) return false;
            ls.Add(off);
        }
        f.Lines = ls;
        return true;
    }

    private static long FixOffset(GoTokenFile f, long offset) => offset < 0 ? 0 : offset > f.Size ? f.Size : offset;
    private static int SearchInts(System.Collections.Generic.List<int> a, long x)
    {
        int i = 0, j = a.Count;
        while (i < j) { int h = (int)((uint)(i + j) >> 1); if (a[h] <= x) i = h + 1; else j = h; }
        return i - 1;
    }
    private static GoPosition Position(GoTokenFile f, long p)
    {
        long offset = FixOffset(f, p - f.Base);
        var pos = new GoPosition { Offset = offset, Filename = f.Name };
        int i = SearchInts(f.Lines, offset);
        if (i >= 0) { pos.Line = i + 1; pos.Column = offset - f.Lines[i] + 1; }
        return pos;
    }

    // ---- Position / Pos ------------------------------------------------------------------
    public static object PositionZero() => new GoPosition();
    public static GoString Position_Filename(object p) => GoString.FromDotNetString(((GoPosition)p).Filename);
    public static long Position_Offset(object p) => ((GoPosition)p).Offset;
    public static long Position_Line(object p) => ((GoPosition)p).Line;
    public static long Position_Column(object p) => ((GoPosition)p).Column;
    public static void Position_SetFilename(object p, GoString v) => ((GoPosition)p).Filename = v.ToDotNetString();
    public static void Position_SetOffset(object p, long v) => ((GoPosition)p).Offset = v;
    public static void Position_SetLine(object p, long v) => ((GoPosition)p).Line = v;
    public static void Position_SetColumn(object p, long v) => ((GoPosition)p).Column = v;

    public static bool Position_IsValid(object p) => ((GoPosition)p).Line > 0;
    public static GoString Position_String(object po)
    {
        var p = (GoPosition)po;
        string s = p.Filename;
        if (p.Line > 0)
        {
            if (s != "") s += ":";
            s += p.Line;
            if (p.Column != 0) s += ":" + p.Column;
        }
        if (s == "") s = "-";
        return GoString.FromDotNetString(s);
    }
    // (token.Pos).IsValid(): a Pos is valid iff it is not NoPos (0).
    public static bool Pos_IsValid(long p) => p != 0;

    private static readonly string[] Tokens = { "ILLEGAL", "EOF", "COMMENT", "", "IDENT", "INT", "FLOAT", "IMAG", "CHAR", "STRING", "", "", "+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "&^", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", "&^=", "&&", "||", "<-", "++", "--", "==", "<", ">", "=", "!", "!=", "<=", ">=", ":=", "...", "(", "[", "{", ",", ".", ")", "]", "}", ";", ":", "", "", "break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough", "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range", "return", "select", "struct", "switch", "type", "var", "", "", "~", "" };
    private const int LiteralBeg=3, LiteralEnd=10, OperatorBeg=11, OperatorEnd=59, KeywordBeg=60, KeywordEnd=86, Tilde=88, Ident=4;
    private static readonly System.Collections.Generic.Dictionary<string,int> Keywords = new() { {"break",61}, {"case",62}, {"chan",63}, {"const",64}, {"continue",65}, {"default",66}, {"defer",67}, {"else",68}, {"fallthrough",69}, {"for",70}, {"func",71}, {"go",72}, {"goto",73}, {"if",74}, {"import",75}, {"interface",76}, {"map",77}, {"package",78}, {"range",79}, {"return",80}, {"select",81}, {"struct",82}, {"switch",83}, {"type",84}, {"var",85} };

    public static GoString Token_String(long tok)
    {
        string s = tok >= 0 && tok < Tokens.Length ? Tokens[tok] : "";
        if (s == "") s = "token(" + tok + ")";
        return GoString.FromDotNetString(s);
    }
    public static bool Token_IsLiteral(long t) => LiteralBeg < t && t < LiteralEnd;
    public static bool Token_IsOperator(long t) => (OperatorBeg < t && t < OperatorEnd) || t == Tilde;
    public static bool Token_IsKeyword(long t) => KeywordBeg < t && t < KeywordEnd;

    public static long Token_Precedence(long op)
    {
        switch (op)
        {
            case 35: return 1;
            case 34: return 2;
            case 39: case 44: case 40: case 45: case 41: case 46: return 3;
            case 12: case 13: case 18: case 19: return 4;
            case 14: case 15: case 16: case 20: case 21: case 17: case 22: return 5;
        }
        return 0;
    }

    public static bool IsKeyword(GoString name) => Keywords.ContainsKey(name.ToDotNetString());
    public static long Lookup(GoString ident) => Keywords.TryGetValue(ident.ToDotNetString(), out var t) ? t : Ident;
    public static bool IsExported(GoString name) { var s = name.ToDotNetString(); return s.Length != 0 && System.Char.IsUpper(s, 0); }
    public static bool IsIdentifier(GoString nameG)
    {
        string name = nameG.ToDotNetString();
        if (name.Length == 0 || Keywords.ContainsKey(name)) return false;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (!System.Char.IsLetter(c) && c != '_' && (i == 0 || !System.Char.IsDigit(c))) return false;
        }
        return true;
    }
}
