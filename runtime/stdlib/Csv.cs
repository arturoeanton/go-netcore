namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A csv.Reader / csv.Writer handle.</summary>
public sealed class GoCsvReader
{
    public string Data = "";
    public byte[]? Raw;
    public char Comma = ',';
    public char Comment;                  // 0 = none
    public bool LazyQuotes;
    public bool TrimLeadingSpace;
    public bool ReuseRecord;              // accepted; Read() always returns a fresh slice here
    public long FieldsPerRecord;          // 0 = set from first record, <0 = no check, >0 = required
    public System.Collections.Generic.List<System.Collections.Generic.List<string>>? Rows;
    public int RowIdx;
    public int Expect = -1;
    // Per-record field positions (1-based line, 1-based byte column) and the input-stream
    // byte offset reached after reading that record. Populated lazily by Csv.EnsurePositions.
    public System.Collections.Generic.List<System.Collections.Generic.List<(int line, int col)>>? Positions;
    public System.Collections.Generic.List<long>? Offsets;
}
public sealed class GoCsvWriter { public object? W; public char Comma = ','; public bool UseCRLF; public System.Text.StringBuilder SB = new(); }

/// <summary>A csv.ParseError: a parse failure tagged with its record/error line and column.</summary>
[GoShim("encoding/csv.ParseError")]
public sealed class GoCsvParseError : IGoError, IGoWrapped
{
    public object? GoUnwrapped() => Err;
    public long StartLine, Line, Column;
    public object? Err;
    public GoString Error()
    {
        string e = Err is IGoError g ? g.Error().ToDotNetString() : "";
        string s;
        if (ReferenceEquals(Err, Csv.ErrFieldCountSentinel)) s = $"record on line {Line}: {e}";
        else if (StartLine != Line) s = $"record on line {StartLine}; parse error on line {Line}, column {Column}: {e}";
        else s = $"parse error on line {Line}, column {Column}: {e}";
        return GoString.FromDotNetString(s);
    }
}

/// <summary>Shim for a subset of Go's <c>encoding/csv</c>.</summary>
public static class Csv
{
    public static object NewReader(object? r)
    {
        var raw = Readers.Drain(r);
        return new GoCsvReader { Raw = raw, Data = System.Text.Encoding.UTF8.GetString(raw) };
    }
    public static object NewWriter(object? w) => new GoCsvWriter { W = w };

    // (*csv.Reader)/(*csv.Writer) configurable field setters. Comma/Comment are runes (int32).
    public static void Reader_SetComma(object r, int v) => ((GoCsvReader)r).Comma = (char)v;
    public static void Reader_SetComment(object r, int v) => ((GoCsvReader)r).Comment = (char)v;
    public static void Reader_SetLazyQuotes(object r, bool v) => ((GoCsvReader)r).LazyQuotes = v;
    public static void Reader_SetTrimLeadingSpace(object r, bool v) => ((GoCsvReader)r).TrimLeadingSpace = v;
    public static void Reader_SetFieldsPerRecord(object r, long v) => ((GoCsvReader)r).FieldsPerRecord = v;
    // ReuseRecord lets Go reuse the returned slice across Read calls (a perf hint); this
    // reader returns a fresh slice each call, so the flag is accepted but has no effect.
    public static void Reader_SetReuseRecord(object r, bool v) => ((GoCsvReader)r).ReuseRecord = v;
    public static int Reader_Comma(object r) => ((GoCsvReader)r).Comma;
    public static long Reader_FieldsPerRecord(object r) => ((GoCsvReader)r).FieldsPerRecord;
    public static void Writer_SetComma(object w, int v) => ((GoCsvWriter)w).Comma = (char)v;
    public static void Writer_SetUseCRLF(object w, bool v) => ((GoCsvWriter)w).UseCRLF = v;

    private static GoSlice Row(System.Collections.Generic.List<string> fields)
    {
        var d = new object?[fields.Count];
        for (int i = 0; i < fields.Count; i++) d[i] = GoString.FromDotNetString(fields[i]);
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    // csv error vars.
    public static readonly GoError ErrBareQuoteSentinel = new(GoString.FromDotNetString("bare \" in non-quoted-field"));
    public static object ErrBareQuote() => ErrBareQuoteSentinel;
    public static readonly GoError ErrQuoteSentinel = new(GoString.FromDotNetString("extraneous or missing \" in quoted-field"));
    public static object ErrQuote() => ErrQuoteSentinel;
    public static readonly GoError ErrFieldCountSentinel = new(GoString.FromDotNetString("wrong number of fields"));
    public static object ErrFieldCount() => ErrFieldCountSentinel;
    public static readonly GoError ErrTrailingCommaSentinel = new(GoString.FromDotNetString("extra delimiter at end of line"));
    public static object ErrTrailingComma() => ErrTrailingCommaSentinel;

    // csv.ParseError struct + methods.
    public static object ParseErrorZero() => new GoCsvParseError();
    public static long ParseError_StartLine(object p) => ((GoCsvParseError)p).StartLine;
    public static long ParseError_Line(object p) => ((GoCsvParseError)p).Line;
    public static long ParseError_Column(object p) => ((GoCsvParseError)p).Column;
    public static object? ParseError_Err(object p) => ((GoCsvParseError)p).Err;
    public static void ParseError_SetStartLine(object p, long v) => ((GoCsvParseError)p).StartLine = v;
    public static void ParseError_SetLine(object p, long v) => ((GoCsvParseError)p).Line = v;
    public static void ParseError_SetColumn(object p, long v) => ((GoCsvParseError)p).Column = v;
    public static void ParseError_SetErr(object p, object? v) => ((GoCsvParseError)p).Err = v;
    public static GoString ParseError_Error(object p) => ((GoCsvParseError)p).Error();
    public static object? ParseError_Unwrap(object p) => ((GoCsvParseError)p).Err;

    // (*csv.Reader).Read() ([]string, error): one record at a time, enforcing the
    // first record's field count (FieldsPerRecord default), io.EOF at end.
    public static object?[] Read(object ro)
    {
        var r = (GoCsvReader)ro;
        r.Rows ??= ParseRows(r);
        if (r.RowIdx >= r.Rows.Count) return new object?[] { default(GoSlice), Io.EOFSentinel };
        var line = r.Rows[r.RowIdx++];
        if (r.FieldsPerRecord >= 0)
        {
            int want = r.FieldsPerRecord > 0 ? (int)r.FieldsPerRecord : r.Expect;
            if (want < 0) { r.Expect = line.Count; want = line.Count; }
            if (line.Count != want)
                // Go returns the parsed record ALONGSIDE the field-count error, not a nil record.
                // The error is a *csv.ParseError wrapping ErrFieldCount, so errors.As/Is work.
                return new object?[] { Row(line), new GoCsvParseError { StartLine = r.RowIdx, Line = r.RowIdx, Column = 1, Err = ErrFieldCountSentinel } };
        }
        return new object?[] { Row(line), null };
    }

    public static object?[] ReadAll(object ro)
    {
        var r = (GoCsvReader)ro;
        var rows = new System.Collections.Generic.List<object?>();
        int expect = r.FieldsPerRecord > 0 ? (int)r.FieldsPerRecord : -1, lineNo = 0;
        foreach (var line in ParseRows(r))
        {
            lineNo++;
            if (r.FieldsPerRecord >= 0)
            {
                if (expect < 0) expect = line.Count; // FieldsPerRecord==0: set from first record
                else if (line.Count != expect)
                    return new object?[] { default(GoSlice), new GoCsvParseError { StartLine = lineNo, Line = lineNo, Column = 1, Err = ErrFieldCountSentinel } };
            }
            rows.Add(Row(line));
        }
        var d = rows.ToArray();
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
    }

    // (*csv.Reader).FieldPos(field) (line, column): the 1-based line and byte-column of the
    // start of the given field in the record most recently returned by Read. Panics on a
    // bad index, matching Go.
    public static object?[] FieldPos(object ro, long field)
    {
        var r = (GoCsvReader)ro;
        EnsurePositions(r);
        int rec = r.RowIdx - 1; // the record most recently returned by Read
        if (rec < 0 || rec >= r.Positions!.Count || field < 0 || field >= r.Positions[rec].Count)
            throw new GoPanicException(GoString.FromDotNetString("out of range index passed to FieldPos"));
        var p = r.Positions[rec][(int)field];
        return new object?[] { (long)p.line, (long)p.col };
    }

    // (*csv.Reader).InputOffset() int64: the input-stream byte offset of the current reader
    // position — the end of the most recently read row / start of the next.
    public static long InputOffset(object ro)
    {
        var r = (GoCsvReader)ro;
        EnsurePositions(r);
        if (r.RowIdx <= 0 || r.Offsets!.Count == 0) return 0;
        int rec = System.Math.Min(r.RowIdx, r.Offsets.Count) - 1;
        return r.Offsets[rec];
    }

    // Faithful port of (*csv.Reader).readRecord position bookkeeping over the buffered input
    // (default options: configured Comma, no Comment/TrimLeadingSpace/LazyQuotes). It records,
    // per record, every field's (line, col) start and the input offset reached after the record.
    private static void EnsurePositions(GoCsvReader r)
    {
        if (r.Positions != null) return;
        var data = r.Raw ?? System.Array.Empty<byte>();
        byte comma = (byte)r.Comma;
        var allPos = new System.Collections.Generic.List<System.Collections.Generic.List<(int, int)>>();
        var offsets = new System.Collections.Generic.List<long>();

        int cursor = 0;
        long offset = 0;
        int numLine = 0;

        // readLine: ReadSlice('\n') equivalent over `data`. Returns the line bytes (with a
        // trailing \r\n normalized to \n, and a lone \r dropped at EOF), advancing cursor,
        // offset (by raw bytes read) and numLine. `eof` is true when this read hit end-of-input.
        byte[]? ReadLine(out bool eof)
        {
            if (cursor >= data.Length) { eof = true; return null; }
            int start = cursor;
            int nl = System.Array.IndexOf(data, (byte)'\n', cursor);
            int end = nl >= 0 ? nl + 1 : data.Length;
            int readSize = end - start;
            cursor = end;
            offset += readSize;
            numLine++;
            eof = nl < 0; // reached EOF without a terminating newline
            var line = new byte[readSize];
            System.Array.Copy(data, start, line, 0, readSize);
            if (eof && readSize > 0 && line[readSize - 1] == (byte)'\r')
            {
                var t = new byte[readSize - 1];
                System.Array.Copy(line, t, readSize - 1);
                line = t;
            }
            int n = line.Length;
            if (n >= 2 && line[n - 2] == (byte)'\r' && line[n - 1] == (byte)'\n')
            {
                var t = new byte[n - 1];
                System.Array.Copy(line, t, n - 1);
                t[n - 2] = (byte)'\n';
                line = t;
            }
            return line;
        }

        int LengthNL(byte[] b, int li) => (b.Length > li && b[b.Length - 1] == (byte)'\n') ? 1 : 0;

        while (true)
        {
            byte[]? line;
            bool eof;
            // Skip blank lines (a line that is empty or just a newline).
            while (true)
            {
                line = ReadLine(out eof);
                if (line == null) break;
                if (line.Length == LengthNL(line, 0)) { if (eof) { line = null; break; } continue; }
                break;
            }
            if (line == null) break;

            var fieldPositions = new System.Collections.Generic.List<(int, int)>();
            int li = 0, posLine = numLine, posCol = 1;
            bool done = false;
            while (!done)
            {
                if (li >= line.Length || line[li] != (byte)'"')
                {
                    // Non-quoted field.
                    int rel = IndexOfByteFrom(line, comma, li);
                    fieldPositions.Add((posLine, posCol));
                    if (rel >= 0) { li += rel + 1; posCol += rel + 1; }
                    else done = true; // end of record (field runs to end of line)
                }
                else
                {
                    // Quoted field.
                    var fieldPos = (posLine, posCol);
                    li++; posCol++; // consume opening quote
                    while (true)
                    {
                        int rel = IndexOfByteFrom(line, (byte)'"', li);
                        if (rel >= 0)
                        {
                            li += rel + 1; posCol += rel + 1; // consume up to and incl. the quote
                            int rn = li < line.Length ? line[li] : -1;
                            if (rn == (byte)'"') { li++; posCol++; } // "" → escaped quote
                            else if (rn == comma) { li++; posCol++; fieldPositions.Add(fieldPos); break; }
                            else if (line.Length - li == LengthNL(line, li)) { fieldPositions.Add(fieldPos); done = true; break; }
                            // else: bare/invalid quote — be lenient and keep scanning the field
                        }
                        else if (li < line.Length)
                        {
                            // Quote spans to next line: advance over the rest, read the next line.
                            posCol += line.Length - li;
                            if (eof) { fieldPositions.Add(fieldPos); done = true; break; }
                            line = ReadLine(out eof); li = 0;
                            if (line == null) { fieldPositions.Add(fieldPos); done = true; break; }
                            if (line.Length > 0) { posLine = numLine; posCol = 1; }
                        }
                        else { fieldPositions.Add(fieldPos); done = true; break; }
                    }
                }
            }
            allPos.Add(fieldPositions);
            offsets.Add(offset);
        }
        r.Positions = allPos;
        r.Offsets = offsets;
    }

    private static int IndexOfByteFrom(byte[] b, byte v, int from)
    {
        for (int i = from; i < b.Length; i++) if (b[i] == v) return i - from;
        return -1;
    }

    // RFC 4180-ish parser (quotes, embedded commas/newlines, doubled quotes).
    private static System.Collections.Generic.List<System.Collections.Generic.List<string>> ParseRows(GoCsvReader r)
        => ParseRows(r.Data, r.Comma, r.Comment, r.TrimLeadingSpace, r.LazyQuotes);

    private static System.Collections.Generic.List<System.Collections.Generic.List<string>> ParseRows(
        string s, char comma, char comment, bool trim, bool lazy)
    {
        var rows = new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
        var row = new System.Collections.Generic.List<string>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false, any = false, fieldStart = true, rowStart = true;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // A line beginning with the Comment character (no leading whitespace) is skipped.
            if (rowStart && comment != '\0' && c == comment)
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue; // rowStart stays true for the next line
            }
            rowStart = false;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"') { field.Append('"'); i++; } // doubled "" -> literal "
                    // LazyQuotes: a " inside a quoted field that is NOT followed by a delimiter
                    // (comma/newline) or EOF is a literal quote; stay in the quoted field.
                    else if (lazy && i + 1 < s.Length && s[i + 1] != comma && s[i + 1] != '\n' && s[i + 1] != '\r') field.Append('"');
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            if (trim && fieldStart && c != comma && (c == ' ' || c == '\t')) continue; // skip leading space
            if (c == '"' && fieldStart) { inQuotes = true; any = true; fieldStart = false; }
            else if (c == '"') { field.Append('"'); any = true; fieldStart = false; } // bare quote: literal (lazy or degraded)
            else if (c == comma) { row.Add(field.ToString()); field.Clear(); any = true; fieldStart = true; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                if (any || field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
                row = new System.Collections.Generic.List<string>(); field.Clear(); any = false; fieldStart = true; rowStart = true;
            }
            else { field.Append(c); any = true; fieldStart = false; }
        }
        if (any || field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    public static object? Write(object wo, GoSlice record)
    {
        var w = (GoCsvWriter)wo;
        for (int i = 0; i < record.Len; i++)
        {
            if (i > 0) w.SB.Append(w.Comma);
            string f = ((GoString)record.Data![record.Off + i]!).ToDotNetString();
            // Go's fieldNeedsQuotes: quote when the field contains the comma/quote/CR/LF, is
            // the literal "\." (confuses some parsers), or BEGINS with whitespace (so leading
            // space is preserved against readers that trim).
            bool needQuotes = f.Length > 0 &&
                (f == "\\." || f.Contains(w.Comma) || f.Contains('"') || f.Contains('\n') || f.Contains('\r') || char.IsWhiteSpace(f[0]));
            if (needQuotes)
                w.SB.Append('"').Append(f.Replace("\"", "\"\"")).Append('"');
            else w.SB.Append(f);
        }
        w.SB.Append(w.UseCRLF ? "\r\n" : "\n");
        return null;
    }
    public static void Flush(object wo) { var w = (GoCsvWriter)wo; Fmt.WriteTo(w.W, w.SB.ToString()); w.SB.Clear(); }

    // (*csv.Writer).WriteAll(records): write each record then flush.
    public static object? WriteAll(object wo, GoSlice records)
    {
        for (int i = 0; i < records.Len; i++)
        {
            var rec = records.Data![records.Off + i];
            if (rec is GoSlice rs) { var e = Write(wo, rs); if (e != null) return e; }
        }
        Flush(wo);
        return null;
    }
    // (*csv.Writer).Error(): the writer never accumulates an error under goclr.
    public static object? Error(object wo) => null;
}
