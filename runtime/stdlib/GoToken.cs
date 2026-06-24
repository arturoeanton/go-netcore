namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>go/token: Token enum methods + identifier/keyword predicates, with the token
/// name table and range markers transcribed from src/go/token/token.go.</summary>
public static class GoToken
{
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
