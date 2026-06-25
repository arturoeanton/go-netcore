namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A text/scanner.Scanner. Ported from src/text/scanner/scanner.go, simplified to
/// scan an eagerly-drained source buffer (so next() needs no refill and token text is always
/// contiguous). The Error/IsIdentRune callbacks are not modelled; the default GoTokens mode
/// and GoWhitespace are used unless Mode/Whitespace are set.</summary>
[GoShim("text/scanner.Scanner")]
public sealed class GoTextScanner
{
    public byte[] Src = System.Array.Empty<byte>();
    public int SrcPos, SrcEnd;
    public int SLine = 1, SColumn, LastLineLen, LastCharLen;
    public int TokPos = -1, TokEnd;
    public int Ch = -2;                         // look-ahead; -2 = none read yet
    public long Mode = GoTokens, ErrorCount;
    public ulong Whitespace = GoWhitespace;
    // The embedded Position (token start), set by Scan.
    public string Filename = "";
    public long POffset, PLine, PColumn;

    // Mode bits are 1<<-tok (Ident=-2 -> ScanIdents=4, etc). GoTokens omits ScanInts (8).
    public const long GoTokens = 4 | 16 | 32 | 64 | 128 | 256 | 512;
    public const ulong GoWhitespace = (1UL << '\t') | (1UL << '\n') | (1UL << '\r') | (1UL << ' ');
    private const int EOF = -1, Ident = -2, Int = -3, Float = -4, Char = -5, Str = -6, RawStr = -7, Comment = -8;
}

public static class Scanner
{
    public static object NewScannerZero() => new GoTextScanner();

    public static object Scanner_Init(object so, object? src)
    {
        var s = (GoTextScanner)so;
        s.Src = Readers.Drain(src);
        s.SrcPos = 0; s.SrcEnd = s.Src.Length;
        s.SLine = 1; s.SColumn = 0; s.LastLineLen = 0; s.LastCharLen = 0;
        s.TokPos = -1; s.Ch = -2;
        s.ErrorCount = 0; s.Mode = GoTextScanner.GoTokens; s.Whitespace = GoTextScanner.GoWhitespace;
        s.PLine = 0;
        return s;
    }

    // ---- field getters (embedded Position is promoted) -----------------------------------
    public static GoString Scanner_Filename(object so) => GoString.FromDotNetString(((GoTextScanner)so).Filename);
    public static void Scanner_SetFilename(object so, GoString v) => ((GoTextScanner)so).Filename = v.ToDotNetString();
    public static long Scanner_PLine(object so) => ((GoTextScanner)so).PLine;
    public static long Scanner_PColumn(object so) => ((GoTextScanner)so).PColumn;
    public static long Scanner_POffset(object so) => ((GoTextScanner)so).POffset;
    // scanner.Mode is a Go `uint`, so the compiler emits/reads it as a ulong; keep the internal
    // bitfield a long (for the bitwise checks) but expose the field as ulong.
    public static ulong Scanner_Mode(object so) => (ulong)((GoTextScanner)so).Mode;
    public static void Scanner_SetMode(object so, ulong v) => ((GoTextScanner)so).Mode = (long)v;
    public static long Scanner_ErrorCount(object so) => ((GoTextScanner)so).ErrorCount;
    public static object Scanner_Position(object so) { var s = (GoTextScanner)so; return new GoPosition { Filename = s.Filename, Offset = s.POffset, Line = s.PLine, Column = s.PColumn }; }

    private static int Next(GoTextScanner s)
    {
        if (s.SrcPos >= s.SrcEnd)
        {
            if (s.LastCharLen > 0) s.SColumn++;
            s.LastCharLen = 0;
            return -1; // EOF
        }
        int ch = s.Src[s.SrcPos], width = 1;
        if (ch >= 0x80)
        {
            (ch, width) = DecodeRune(s.Src, s.SrcPos, s.SrcEnd);
            if (ch == 0xFFFD && width == 1) { s.SrcPos += 1; s.LastCharLen = 1; s.SColumn++; s.ErrorCount++; return 0xFFFD; }
        }
        s.SrcPos += width; s.LastCharLen = width; s.SColumn++;
        if (ch == 0) s.ErrorCount++;
        else if (ch == '\n') { s.SLine++; s.LastLineLen = s.SColumn; s.SColumn = 0; }
        return ch;
    }

    private static int Peek(GoTextScanner s)
    {
        if (s.Ch == -2)
        {
            s.Ch = Next(s);
            if (s.Ch == 0xFEFF) s.Ch = Next(s); // ignore BOM
        }
        return s.Ch;
    }

    public static int Scanner_Next(object so)
    {
        var s = (GoTextScanner)so;
        s.TokPos = -1; s.PLine = 0;
        int ch = Peek(s);
        if (ch != -1) s.Ch = Next(s);
        return ch;
    }
    public static int Scanner_Peek(object so) => Peek((GoTextScanner)so);

    public static object Scanner_Pos(object so)
    {
        var s = (GoTextScanner)so;
        var pos = new GoPosition { Filename = s.Filename, Offset = s.SrcPos - s.LastCharLen };
        if (s.SColumn > 0) { pos.Line = s.SLine; pos.Column = s.SColumn; }
        else if (s.LastLineLen > 0) { pos.Line = s.SLine - 1; pos.Column = s.LastLineLen; }
        else { pos.Line = 1; pos.Column = 1; }
        return pos;
    }

    public static GoString Scanner_TokenText(object so)
    {
        var s = (GoTextScanner)so;
        if (s.TokPos < 0) return GoString.FromDotNetString("");
        if (s.TokEnd < s.TokPos) s.TokEnd = s.TokPos;
        return GoString.FromBytes(Sub(s.Src, s.TokPos, s.TokEnd));
    }

    public static int Scanner_Scan(object so)
    {
        var s = (GoTextScanner)so;
        int ch = Peek(s);
        s.TokPos = -1; s.PLine = 0;
    redo:
        while (ch >= 0 && ch <= 0x3F && (s.Whitespace & (1UL << ch)) != 0) ch = Next(s);
        s.TokPos = s.SrcPos - s.LastCharLen;
        s.POffset = s.TokPos;
        if (s.SColumn > 0) { s.PLine = s.SLine; s.PColumn = s.SColumn; }
        else { s.PLine = s.SLine - 1; s.PColumn = s.LastLineLen; }

        long tok = ch;
        if (IsIdentRune(s, ch, 0))
        {
            if ((s.Mode & ScanIdents()) != 0) { tok = -2; ch = ScanIdentifier(s); }
            else ch = Next(s);
        }
        else if (IsDecimal(ch))
        {
            if ((s.Mode & (ScanIntsM() | ScanFloatsM())) != 0) { var r = ScanNumber(s, ch, false); tok = r.tok; ch = r.ch; }
            else ch = Next(s);
        }
        else
        {
            switch (ch)
            {
                case -1: break;
                case '"': if ((s.Mode & ScanStringsM()) != 0) { ScanString(s, '"'); tok = -6; } ch = Next(s); break;
                case '\'': if ((s.Mode & ScanCharsM()) != 0) { ScanChar(s); tok = -5; } ch = Next(s); break;
                case '.':
                    ch = Next(s);
                    if (IsDecimal(ch) && (s.Mode & ScanFloatsM()) != 0) { var r = ScanNumber(s, ch, true); tok = r.tok; ch = r.ch; }
                    break;
                case '/':
                    ch = Next(s);
                    if ((ch == '/' || ch == '*') && (s.Mode & ScanCommentsM()) != 0)
                    {
                        if ((s.Mode & SkipCommentsM()) != 0) { s.TokPos = -1; ch = ScanComment(s, ch); goto redo; }
                        ch = ScanComment(s, ch); tok = -8;
                    }
                    break;
                case '`': if ((s.Mode & ScanRawStringsM()) != 0) { ScanRawString(s); tok = -7; } ch = Next(s); break;
                default: ch = Next(s); break;
            }
        }
        s.TokEnd = s.SrcPos - s.LastCharLen;
        s.Ch = ch;
        return (int)tok;
    }

    // ---- token scanners ------------------------------------------------------------------
    private static int ScanIdentifier(GoTextScanner s)
    {
        int ch = Next(s);
        for (int i = 1; IsIdentRune(s, ch, i); i++) ch = Next(s);
        return ch;
    }
    private static bool IsIdentRune(GoTextScanner s, int ch, int i) =>
        ch == '_' || (ch >= 0 && char.IsLetter((char)Bmp(ch))) || (ch >= 0 && char.IsDigit((char)Bmp(ch)) && i > 0);
    private static int Bmp(int ch) => ch <= 0xFFFF ? ch : 'A'; // letters/digits we test are BMP; non-BMP letters are rare
    private static bool IsDecimal(int ch) => '0' <= ch && ch <= '9';
    private static bool IsHex(int ch) => ('0' <= ch && ch <= '9') || ('a' <= Lower(ch) && Lower(ch) <= 'f');
    private static int Lower(int ch) => ('a' - 'A') | ch;

    private static (int ch, int digsep) Digits(GoTextScanner s, int ch0, int base_)
    {
        int ch = ch0, digsep = 0;
        if (base_ <= 10)
        {
            for (; IsDecimal(ch) || ch == '_';) { digsep |= ch == '_' ? 2 : 1; ch = Next(s); }
        }
        else
        {
            for (; IsHex(ch) || ch == '_';) { digsep |= ch == '_' ? 2 : 1; ch = Next(s); }
        }
        return (ch, digsep);
    }

    private static (long tok, int ch) ScanNumber(GoTextScanner s, int ch, bool seenDot)
    {
        int base_ = 10, prefix = 0, digsep = 0;
        long tok = 0;
        if (!seenDot)
        {
            tok = -3; // Int
            if (ch == '0')
            {
                ch = Next(s);
                switch (Lower(ch)) { case 'x': ch = Next(s); base_ = 16; prefix = 'x'; break; case 'o': ch = Next(s); base_ = 8; prefix = 'o'; break; case 'b': ch = Next(s); base_ = 2; prefix = 'b'; break; default: base_ = 8; prefix = '0'; digsep = 1; break; }
            }
            var d = Digits(s, ch, base_); ch = d.ch; digsep |= d.digsep;
            if (ch == '.' && (s.Mode & ScanFloatsM()) != 0) { ch = Next(s); seenDot = true; }
        }
        if (seenDot)
        {
            tok = -4; // Float
            var d = Digits(s, ch, base_); ch = d.ch; digsep |= d.digsep;
        }
        int e = Lower(ch);
        if ((e == 'e' || e == 'p') && (s.Mode & ScanFloatsM()) != 0)
        {
            ch = Next(s); tok = -4;
            if (ch == '+' || ch == '-') ch = Next(s);
            var d = Digits(s, ch, 10); ch = d.ch;
        }
        return (tok, ch);
    }

    private static void ScanString(GoTextScanner s, int quote)
    {
        int ch = Next(s);
        while (ch != quote)
        {
            if (ch == '\n' || ch < 0) { s.ErrorCount++; return; }
            ch = ch == '\\' ? ScanEscape(s, quote) : Next(s);
        }
    }
    private static int ScanStringCount(GoTextScanner s, int quote)
    {
        int ch = Next(s), n = 0;
        while (ch != quote)
        {
            if (ch == '\n' || ch < 0) { s.ErrorCount++; return n; }
            ch = ch == '\\' ? ScanEscape(s, quote) : Next(s);
            n++;
        }
        return n;
    }
    private static void ScanChar(GoTextScanner s) { if (ScanStringCount(s, '\'') != 1) s.ErrorCount++; }
    private static void ScanRawString(GoTextScanner s)
    {
        int ch = Next(s);
        while (ch != '`') { if (ch < 0) { s.ErrorCount++; return; } ch = Next(s); }
    }
    private static int ScanEscape(GoTextScanner s, int quote)
    {
        int ch = Next(s);
        switch (ch)
        {
            case 'a': case 'b': case 'f': case 'n': case 'r': case 't': case 'v': case '\\': ch = Next(s); break;
            case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': ch = ScanDigits(s, ch, 8, 3); break;
            case 'x': ch = ScanDigits(s, Next(s), 16, 2); break;
            case 'u': ch = ScanDigits(s, Next(s), 16, 4); break;
            case 'U': ch = ScanDigits(s, Next(s), 16, 8); break;
            default: if (ch == quote) ch = Next(s); else s.ErrorCount++; break;
        }
        return ch;
    }
    private static int ScanDigits(GoTextScanner s, int ch, int base_, int n)
    {
        for (; n > 0 && DigitVal(ch) < base_;) { ch = Next(s); n--; }
        if (n > 0) s.ErrorCount++;
        return ch;
    }
    private static int DigitVal(int ch) => ('0' <= ch && ch <= '9') ? ch - '0' : ('a' <= Lower(ch) && Lower(ch) <= 'f') ? Lower(ch) - 'a' + 10 : 16;
    private static int ScanComment(GoTextScanner s, int ch)
    {
        if (ch == '/') { ch = Next(s); while (ch != '\n' && ch >= 0) ch = Next(s); return ch; }
        ch = Next(s);
        while (true)
        {
            if (ch < 0) { s.ErrorCount++; break; }
            int ch0 = ch; ch = Next(s);
            if (ch0 == '*' && ch == '/') { ch = Next(s); break; }
        }
        return ch;
    }

    private static int ScanIdents() => 4; private static int ScanIntsM() => 8; private static int ScanFloatsM() => 16;
    private static int ScanCharsM() => 32; private static int ScanStringsM() => 64; private static int ScanRawStringsM() => 128;
    private static int ScanCommentsM() => 256; private static int SkipCommentsM() => 512;

    private static (int, int) DecodeRune(byte[] s, int i, int end)
    {
        byte b = s[i];
        if (b < 0x80) return (b, 1);
        int n = b < 0xC0 ? 1 : b < 0xE0 ? 2 : b < 0xF0 ? 3 : b < 0xF8 ? 4 : 1;
        if (n == 1 || i + n > end) return (0xFFFD, 1);
        int cp = b & (0x7F >> n);
        for (int k = 1; k < n; k++) { if ((s[i + k] & 0xC0) != 0x80) return (0xFFFD, 1); cp = (cp << 6) | (s[i + k] & 0x3F); }
        return (cp, n);
    }
    private static byte[] Sub(byte[] s, int from, int to) { var r = new byte[to - from]; System.Array.Copy(s, from, r, 0, to - from); return r; }
}
