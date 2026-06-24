namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A csv.Reader / csv.Writer handle.</summary>
public sealed class GoCsvReader
{
    public string Data = "";
    public char Comma = ',';
    public System.Collections.Generic.List<System.Collections.Generic.List<string>>? Rows;
    public int RowIdx;
    public int Expect = -1;
}
public sealed class GoCsvWriter { public object? W; public char Comma = ','; public System.Text.StringBuilder SB = new(); }

/// <summary>Shim for a subset of Go's <c>encoding/csv</c>.</summary>
public static class Csv
{
    public static object NewReader(object? r) => new GoCsvReader { Data = System.Text.Encoding.UTF8.GetString(Readers.Drain(r)) };
    public static object NewWriter(object? w) => new GoCsvWriter { W = w };

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

    // (*csv.Reader).Read() ([]string, error): one record at a time, enforcing the
    // first record's field count (FieldsPerRecord default), io.EOF at end.
    public static object?[] Read(object ro)
    {
        var r = (GoCsvReader)ro;
        r.Rows ??= ParseRows(r.Data, r.Comma);
        if (r.RowIdx >= r.Rows.Count) return new object?[] { default(GoSlice), Io.EOFSentinel };
        var line = r.Rows[r.RowIdx++];
        if (r.Expect < 0) r.Expect = line.Count;
        else if (line.Count != r.Expect)
            return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("record on line " + r.RowIdx + ": wrong number of fields")) };
        return new object?[] { Row(line), null };
    }

    public static object?[] ReadAll(object ro)
    {
        var r = (GoCsvReader)ro;
        var rows = new System.Collections.Generic.List<object?>();
        int expect = -1, lineNo = 0;
        foreach (var line in ParseRows(r.Data, r.Comma))
        {
            lineNo++;
            if (expect < 0) expect = line.Count; // Go enforces FieldsPerRecord
            else if (line.Count != expect)
                return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("record on line " + lineNo + ": wrong number of fields")) };
            rows.Add(Row(line));
        }
        var d = rows.ToArray();
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
    }

    // RFC 4180-ish parser (quotes, embedded commas/newlines, doubled quotes).
    private static System.Collections.Generic.List<System.Collections.Generic.List<string>> ParseRows(string s, char comma)
    {
        var rows = new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
        var row = new System.Collections.Generic.List<string>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false, any = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inQuotes)
            {
                if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { field.Append('"'); i++; } else inQuotes = false; }
                else field.Append(c);
            }
            else if (c == '"') { inQuotes = true; any = true; }
            else if (c == comma) { row.Add(field.ToString()); field.Clear(); any = true; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                if (any || field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
                row = new System.Collections.Generic.List<string>(); field.Clear(); any = false;
            }
            else { field.Append(c); any = true; }
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
            if (f.Contains(w.Comma) || f.Contains('"') || f.Contains('\n') || f.Contains('\r'))
                w.SB.Append('"').Append(f.Replace("\"", "\"\"")).Append('"');
            else w.SB.Append(f);
        }
        w.SB.Append('\n');
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
