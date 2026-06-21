namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>time</c> package: Duration helpers and
/// Sleep. (time.Time value type is pending the opaque-value-type pattern.)</summary>
public static class Time
{
    private const long Nanosecond = 1, Microsecond = 1000, Millisecond = 1000000,
        Second = 1000000000, Minute = 60 * Second, Hour = 60 * Minute;

    public static void Sleep(long d)
    {
        if (d <= 0) return;
        System.Threading.Thread.Sleep((int)(d / Millisecond));
    }

    // time.After(d) <-chan Time: a buffered channel that receives the current time
    // after d, driven by a background timer.
    public static GoChan After(long d)
    {
        var ch = GoChans.Make(1);
        int ms = d <= 0 ? 0 : (int)(d / Millisecond);
        System.Threading.Tasks.Task.Run(() =>
        {
            if (ms > 0) System.Threading.Thread.Sleep(ms);
            GoChans.Send(ch, Now());
        });
        return ch;
    }

    // time.NewTicker(d): a *Ticker whose C channel receives the time every d. The
    // send is non-blocking (capacity 1), so slow receivers drop ticks, like Go.
    public static object NewTicker(long d)
    {
        var t = new GoTicker { C = GoChans.Make(1) };
        int ms = d <= 0 ? 1 : (int)(d / Millisecond);
        t.Timer = new System.Threading.Timer(_ => t.C.TrySend(Now()), null, ms, ms);
        return t;
    }
    // time.NewTimer(d): a *Timer that fires C once after d.
    public static object NewTimer(long d)
    {
        var t = new GoTicker { C = After(d) };
        return t;
    }
    // time.AfterFunc(d, f): run f in its own goroutine after d; returns a *Timer.
    public static object AfterFunc(long d, GoClosure f)
    {
        long ms = d / Millisecond;
        if (ms < 0) ms = 0;
        var t = new GoTicker();
        t.Timer = new System.Threading.Timer(_ => { try { GoRuntime.InvokeArgs(f); } catch { } },
            null, ms, System.Threading.Timeout.Infinite);
        return t;
    }
    // time.Tick(d): just the channel of a new ticker (the ticker is never collected).
    public static GoChan Tick(long d) => ((GoTicker)NewTicker(d)).C;

    public static GoChan Ticker_C(object t) => ((GoTicker)t).C;
    public static void Ticker_Stop(object t) => ((GoTicker)t).Timer?.Dispose();        // *Ticker.Stop() is void
    public static bool Timer_Stop(object t) { ((GoTicker)t).Timer?.Dispose(); return true; } // *Timer.Stop() bool
    public static void Ticker_Reset(object t, long d) { }                               // *Ticker.Reset(d) is void
    public static bool Timer_Reset(object t, long d) => true;                            // *Timer.Reset(d) returns bool

    // time.Month / time.Weekday String() (named int types).
    public static GoString Month_String(long m) => GoString.FromDotNetString(m >= 1 && m <= 12 ? MonthsLong[m - 1] : "%!Month(" + m + ")");
    public static GoString Weekday_String(long w) => GoString.FromDotNetString(w >= 0 && w <= 6 ? DaysLong[w] : "%!Weekday(" + w + ")");

    // time.Duration methods (receiver is the int64 nanosecond count).
    public static double Duration_Seconds(long d) => (double)d / Second;
    public static double Duration_Minutes(long d) => (double)d / Minute;
    public static double Duration_Hours(long d) => (double)d / Hour;
    public static long Duration_Nanoseconds(long d) => d;
    public static long Duration_Microseconds(long d) => d / Microsecond;
    public static long Duration_Milliseconds(long d) => d / Millisecond;

    public static long Duration_Truncate(long d, long m) => m <= 0 ? d : d - d % m;
    public static long Duration_Round(long d, long m)
    {
        if (m <= 0) return d;
        long r = d % m;
        if (d < 0) { r = -r; if (r + r < m) return d + r; long d1 = d - (m - r); return d1 < d ? d1 : long.MinValue; }
        if (r + r < m) return d - r;
        long d2 = d + (m - r); return d2 > d ? d2 : long.MaxValue;
    }

    // Duration.String() — a faithful integer port of Go's time/time.go (no float64,
    // so durations above 2^53 ns stay exact).
    public static GoString Duration_String(long d)
    {
        var buf = new byte[40];
        int w = buf.Length;
        bool neg = d < 0;
        ulong u = unchecked((ulong)d);
        if (neg) u = unchecked(0 - u);

        if (u < (ulong)Second)
        {
            int prec;
            w--; buf[w] = (byte)'s';
            w--;
            if (u == 0) return GoString.FromDotNetString("0s");
            if (u < (ulong)Microsecond) { prec = 0; buf[w] = (byte)'n'; }
            else if (u < (ulong)Millisecond) { prec = 3; w--; buf[w] = 0xC2; buf[w + 1] = 0xB5; } // "µ"
            else { prec = 6; buf[w] = (byte)'m'; }
            (w, u) = FmtFrac(buf, w, u, prec);
            w = FmtInt(buf, w, u);
        }
        else
        {
            w--; buf[w] = (byte)'s';
            (w, u) = FmtFrac(buf, w, u, 9);
            w = FmtInt(buf, w, u % 60);
            u /= 60;
            if (u > 0)
            {
                w--; buf[w] = (byte)'m';
                w = FmtInt(buf, w, u % 60);
                u /= 60;
                if (u > 0) { w--; buf[w] = (byte)'h'; w = FmtInt(buf, w, u); }
            }
        }
        if (neg) { w--; buf[w] = (byte)'-'; }
        return GoString.FromDotNetString(System.Text.Encoding.UTF8.GetString(buf, w, buf.Length - w));
    }

    // Print u/10^prec into buf ending at w, trimming trailing zeros; returns (newW, intPart).
    private static (int, ulong) FmtFrac(byte[] buf, int w, ulong v, int prec)
    {
        bool print = false;
        for (int i = 0; i < prec; i++)
        {
            int digit = (int)(v % 10);
            print = print || digit != 0;
            if (print) { w--; buf[w] = (byte)('0' + digit); }
            v /= 10;
        }
        if (print) { w--; buf[w] = (byte)'.'; }
        return (w, v);
    }

    private static int FmtInt(byte[] buf, int w, ulong v)
    {
        if (v == 0) { w--; buf[w] = (byte)'0'; }
        else while (v > 0) { w--; buf[w] = (byte)('0' + (int)(v % 10)); v /= 10; }
        return w;
    }

    // ---- time.Time (UTC value type backed by Unix nanoseconds) -------------

    private static readonly System.DateTime Epoch =
        new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

    private static System.DateTime ToDateTime(GoTime t) => Epoch.AddTicks(t.N / 100);
    private static GoTime FromDateTime(System.DateTime dt) =>
        new GoTime { N = (dt.ToUniversalTime() - Epoch).Ticks * 100, IsZero = false };

    // Construction.
    public static object Now() => new GoTime { N = (System.DateTime.UtcNow - Epoch).Ticks * 100, IsZero = false };
    public static object Unix(long sec, long nsec) => new GoTime { N = sec * Second + nsec, IsZero = false };
    public static object Date(long year, long month, long day, long hour, long min, long sec, long nsec, object? loc)
    {
        var dt = new System.DateTime((int)year, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
            .AddMonths((int)month - 1).AddDays(day - 1)
            .AddHours(hour).AddMinutes(min).AddSeconds(sec);
        var t = FromDateTime(dt); t.N += nsec; return t;
    }
    public static long Since(object t) => (System.DateTime.UtcNow - Epoch).Ticks * 100 - ((GoTime)t).N;

    // Methods (receiver passed as first arg).
    public static long Time_Unix(object t) => ((GoTime)t).N / Second;
    public static long Time_UnixNano(object t) => ((GoTime)t).N;
    public static long Time_UnixMilli(object t) => ((GoTime)t).N / Millisecond;
    public static long Time_Year(object t) => ZeroDate(t, dt => dt.Year, 1);
    public static long Time_YearDay(object t) => ZeroDate(t, dt => dt.DayOfYear, 1);
    // UTC-only: the zone is always UTC with a zero offset.
    public static object?[] Time_Zone(object t) => new object?[] { GoString.FromDotNetString("UTC"), 0L };
    public static object Time_In(object t, object loc) => t;       // UTC-only: location is ignored
    public static object Time_Location(object t) => UTC();
    public static long Time_Month(object t) => ZeroDate(t, dt => dt.Month, 1);
    public static long Time_Day(object t) => ZeroDate(t, dt => dt.Day, 1);
    public static long Time_Hour(object t) => ZeroDate(t, dt => dt.Hour, 0);
    public static long Time_Minute(object t) => ZeroDate(t, dt => dt.Minute, 0);
    public static long Time_Second(object t) => ZeroDate(t, dt => dt.Second, 0);
    public static long Time_Nanosecond(object t) => ((GoTime)t).N % Second;
    public static long Time_Weekday(object t) => ZeroDate(t, dt => (int)dt.DayOfWeek, 1);
    public static object Time_Add(object t, long d) => new GoTime { N = ((GoTime)t).N + d, IsZero = false };
    public static object Time_Round(object t, long d) { var n = ((GoTime)t).N; if (d <= 0) return new GoTime { N = n, IsZero = ((GoTime)t).IsZero }; long r = n % d; long h = d / 2; n = r + r < d ? n - r : n - r + d; return new GoTime { N = n, IsZero = false }; }
    public static object Time_Truncate(object t, long d) { var n = ((GoTime)t).N; if (d <= 0) return new GoTime { N = n, IsZero = ((GoTime)t).IsZero }; return new GoTime { N = n - n % d, IsZero = false }; }
    public static long Time_Sub(object t, object u) => ((GoTime)t).N - ((GoTime)u).N;
    public static bool Time_Before(object t, object u) => ((GoTime)t).N < ((GoTime)u).N;
    public static bool Time_After(object t, object u) => ((GoTime)t).N > ((GoTime)u).N;
    public static bool Time_Equal(object t, object u) => ((GoTime)t).N == ((GoTime)u).N;
    public static bool Time_IsZero(object t) => ((GoTime)t).IsZero;
    public static object Time_UTC(object t) => t;
    public static object Time_Local(object t) => t;
    public static GoString Time_String(object t) => GoString.FromDotNetString(DoFormat((GoTime)t, "2006-01-02 15:04:05.999999999 -0700 MST"));
    public static GoString Time_Format(object t, GoString layout) => GoString.FromDotNetString(DoFormat((GoTime)t, layout.ToDotNetString()));
    // (time.Time).AppendFormat(b, layout): append the formatted time to the byte slice.
    public static GoSlice Time_AppendFormat(object t, GoSlice b, GoString layout)
    {
        var by = System.Text.Encoding.UTF8.GetBytes(DoFormat((GoTime)t, layout.ToDotNetString()));
        int n = b.Len;
        var data = new object?[n + by.Length];
        for (int i = 0; i < n; i++) data[i] = b.Data![b.Off + i];
        for (int i = 0; i < by.Length; i++) data[n + i] = (int)by[i];
        return new GoSlice { Data = data, Off = 0, Len = data.Length, Cap = data.Length };
    }

    // time.Parse(layout, value) (Time, error): the inverse of Format — walk the Go
    // reference-time layout, consuming the matching run from value for each token.
    // Returns the zero Time and an error if value does not match the layout. The
    // result is in UTC (goclr's time is UTC-only; see LIMITATIONS.md).
    public static object?[] Parse(GoString layout, GoString value)
    {
        string lay = layout.ToDotNetString(), val = value.ToDotNetString();
        // Go defaults a missing year to 0; goclr's GoTime counts nanoseconds from the
        // Unix epoch (representable range ~1678..2262), so a yearless layout uses 1970
        // to keep the parsed clock fields exact without overflowing (see LIMITATIONS.md).
        int year = 1970, month = 1, day = 1, hour = 0, min = 0, sec = 0, nsec = 0;
        bool hasPM = false, pm = false;
        int li = 0, vi = 0;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        bool Tok(string tok) =>
            li + tok.Length <= lay.Length && string.CompareOrdinal(lay, li, tok, 0, tok.Length) == 0 && (li += tok.Length) >= 0;
        int ReadInt(int max)
        {
            if (vi < val.Length && val[vi] == ' ') vi++; // tolerate space padding (_2)
            int start = vi, n = 0;
            while (vi < val.Length && n < max && char.IsDigit(val[vi])) { vi++; n++; }
            if (vi == start) throw new System.FormatException();
            return int.Parse(val.Substring(start, vi - start), inv);
        }
        int ReadName(string[] names)
        {
            for (int m = 0; m < names.Length; m++)
                if (vi + names[m].Length <= val.Length && string.CompareOrdinal(val, vi, names[m], 0, names[m].Length) == 0)
                { vi += names[m].Length; return m; }
            throw new System.FormatException();
        }
        void ReadFrac()
        {
            if (vi < val.Length && val[vi] == '.')
            {
                vi++;
                int s = vi;
                while (vi < val.Length && char.IsDigit(val[vi])) vi++;
                string d = val.Substring(s, vi - s);
                if (d.Length > 9) d = d.Substring(0, 9);
                if (d.Length > 0) nsec = int.Parse(d.PadRight(9, '0'), inv);
            }
        }
        void SkipZone()
        {
            if (vi < val.Length && (val[vi] == 'Z')) { vi++; return; }
            while (vi < val.Length && (val[vi] == '+' || val[vi] == '-' || val[vi] == ':' || char.IsDigit(val[vi]))) vi++;
        }

        try
        {
            while (li < lay.Length)
            {
                if (Tok("2006")) year = ReadInt(4);
                else if (Tok("06")) year = 2000 + ReadInt(2);
                else if (Tok("January")) month = ReadName(MonthsLong) + 1;
                else if (Tok("Jan")) month = ReadName(MonthsAbbr) + 1;
                else if (Tok("01")) month = ReadInt(2);
                else if (Tok("Monday")) ReadName(DaysLong);
                else if (Tok("Mon")) ReadName(DaysAbbr);
                else if (Tok("02") || Tok("_2")) day = ReadInt(2);
                else if (Tok("15")) hour = ReadInt(2);
                else if (Tok("03")) hour = ReadInt(2);
                else if (Tok("04")) min = ReadInt(2);
                else if (Tok("05")) sec = ReadInt(2);
                else if (Tok("PM") || Tok("pm"))
                {
                    hasPM = true;
                    if (vi + 2 <= val.Length) { pm = val.Substring(vi, 2).ToUpperInvariant() == "PM"; vi += 2; }
                }
                else if (Tok(".000000000") || Tok(".000000") || Tok(".000") ||
                         Tok(".999999999") || Tok(".999999") || Tok(".999")) ReadFrac();
                else if (Tok("Z07:00") || Tok("Z0700") || Tok("Z07")) SkipZone();
                else if (Tok("-07:00") || Tok("-0700") || Tok("-07")) SkipZone();
                else if (Tok("MST")) { while (vi < val.Length && char.IsLetter(val[vi])) vi++; }
                else if (Tok("3")) hour = ReadInt(2);
                else if (Tok("2")) day = ReadInt(2);
                else if (Tok("1")) month = ReadInt(2);
                else if (Tok("4")) min = ReadInt(2);
                else if (Tok("5")) sec = ReadInt(2);
                else
                {
                    if (vi < val.Length && val[vi] == lay[li]) { vi++; li++; }
                    else throw new System.FormatException();
                }
            }
            if (hasPM) { if (pm && hour < 12) hour += 12; else if (!pm && hour == 12) hour = 0; }
            var dt = new System.DateTime(year, month, day, hour, min, sec, System.DateTimeKind.Utc);
            var t = FromDateTime(dt);
            t.N += nsec;
            return new object?[] { t, null };
        }
        catch
        {
            return new object?[] { new GoTime { IsZero = true },
                new GoError("parsing time \"" + val + "\" as \"" + lay + "\": cannot parse") };
        }
    }

    private static long ZeroDate(object t, System.Func<System.DateTime, int> f, int zero)
        => ((GoTime)t).IsZero ? zero : f(ToDateTime((GoTime)t));

    private static readonly string[] MonthsLong = { "January","February","March","April","May","June","July","August","September","October","November","December" };
    private static readonly string[] MonthsAbbr = { "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };
    private static readonly string[] DaysLong = { "Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday" };
    private static readonly string[] DaysAbbr = { "Sun","Mon","Tue","Wed","Thu","Fri","Sat" };

    private static string DoFormat(GoTime t, string layout)
    {
        System.DateTime dt = t.IsZero ? new System.DateTime(1, 1, 1, 0, 0, 0, System.DateTimeKind.Utc) : ToDateTime(t);
        long nanos = t.IsZero ? 0 : ((t.N % Second) + Second) % Second;
        var sb = new System.Text.StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int i = 0;
        while (i < layout.Length)
        {
            bool M(string tok) { if (string.CompareOrdinal(layout, i, tok, 0, tok.Length) == 0) { i += tok.Length; return true; } return false; }
            if (M("2006")) { sb.Append(dt.Year.ToString("D4", inv)); }
            else if (M("06")) { sb.Append((dt.Year % 100).ToString("D2", inv)); }
            else if (M("January")) { sb.Append(MonthsLong[dt.Month - 1]); }
            else if (M("Jan")) { sb.Append(MonthsAbbr[dt.Month - 1]); }
            else if (M("01")) { sb.Append(dt.Month.ToString("D2", inv)); }
            else if (M("Monday")) { sb.Append(DaysLong[(int)dt.DayOfWeek]); }
            else if (M("Mon")) { sb.Append(DaysAbbr[(int)dt.DayOfWeek]); }
            else if (M("02")) { sb.Append(dt.Day.ToString("D2", inv)); }
            else if (M("_2")) { sb.Append(dt.Day.ToString(inv).PadLeft(2)); }
            else if (M("15")) { sb.Append(dt.Hour.ToString("D2", inv)); }
            else if (M("03")) { sb.Append((((dt.Hour + 11) % 12) + 1).ToString("D2", inv)); }
            else if (M("3")) { sb.Append((((dt.Hour + 11) % 12) + 1).ToString(inv)); }
            else if (M("04")) { sb.Append(dt.Minute.ToString("D2", inv)); }
            else if (M("05")) { sb.Append(dt.Second.ToString("D2", inv)); }
            else if (M("PM")) { sb.Append(dt.Hour < 12 ? "AM" : "PM"); }
            else if (M("pm")) { sb.Append(dt.Hour < 12 ? "am" : "pm"); }
            else if (M(".999999999")) { if (nanos != 0) sb.Append('.').Append(nanos.ToString("D9", inv).TrimEnd('0')); }
            else if (M(".000000000")) { sb.Append('.').Append(nanos.ToString("D9", inv)); }
            else if (M(".000")) { sb.Append('.').Append((nanos / 1000000).ToString("D3", inv)); }
            else if (M("Z07:00")) { sb.Append('Z'); }
            else if (M("Z0700")) { sb.Append('Z'); }
            else if (M("-07:00")) { sb.Append("+00:00"); }
            else if (M("-0700")) { sb.Append("+0000"); }
            else if (M("MST")) { sb.Append("UTC"); }
            else if (M("2")) { sb.Append(dt.Day.ToString(inv)); }
            else if (M("1")) { sb.Append(dt.Month.ToString(inv)); }
            else if (M("4")) { sb.Append(dt.Minute.ToString(inv)); }
            else if (M("5")) { sb.Append(dt.Second.ToString(inv)); }
            else { sb.Append(layout[i]); i++; }
        }
        return sb.ToString();
    }

    // time zero value (Go zero time: 0001-01-01 00:00:00 UTC).
    public static object TimeZero() => new GoTime { N = 0, IsZero = true };

    // time.UTC / time.Local package variables (locations are treated as UTC).
    public static object UTC() => UtcLoc;
    public static object Local() => UtcLoc;
    // time.FixedZone(name, offsetSeconds): a Location at a constant UTC offset.
    public static object FixedZone(GoString name, long offsetSec) =>
        new GoLocation { Name = name.ToDotNetString(), OffsetSeconds = (int)offsetSec };

    // time.ParseDuration("1h30m", "300ms", …) (Duration, error): sum of decimal runs
    // each followed by a unit (ns/us/µs/ms/s/m/h), with an optional leading sign.
    public static object?[] ParseDuration(GoString sg)
    {
        string s = sg.ToDotNetString();
        if (s == "0") return new object?[] { 0L, null };
        int i = 0;
        long total = 0;
        bool neg = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) { neg = s[i] == '-'; i++; }
        if (i >= s.Length) return Err(s);
        while (i < s.Length)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i == start) return Err(s);
            if (!double.TryParse(s.Substring(start, i - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                return Err(s);
            int us = i;
            while (i < s.Length && !char.IsDigit(s[i]) && s[i] != '.') i++;
            string unit = s.Substring(us, i - us);
            long scale = unit switch
            {
                "ns" => Nanosecond, "us" => Microsecond, "µs" => Microsecond, "μs" => Microsecond,
                "ms" => Millisecond, "s" => Second, "m" => Minute, "h" => Hour,
                _ => -1,
            };
            if (scale < 0) return Err(s);
            total += (long)(val * scale);
        }
        return new object?[] { neg ? -total : total, null };
    }
    private static object?[] Err(string s) => new object?[] { 0L, new GoError("time: invalid duration \"" + s + "\"") };

    // time.ParseInLocation(layout, value, loc): goclr's time is UTC-only, so the
    // location is ignored and this is Parse.
    public static object?[] ParseInLocation(GoString layout, GoString value, object loc) => Parse(layout, value);

    // time.LoadLocation(name) (*Location, error): "" and "UTC" are UTC; otherwise look
    // up the IANA/system zone for its base offset (goclr's time is UTC-only, so the
    // zone carries just a name and fixed offset — adequate for validation/formatting).
    public static object?[] LoadLocation(GoString name)
    {
        string n = name.ToDotNetString();
        if (n.Length == 0 || n == "UTC") return new object?[] { UtcLoc, null };
        try
        {
            var tz = System.TimeZoneInfo.FindSystemTimeZoneById(n);
            return new object?[] { new GoLocation { Name = n, OffsetSeconds = (int)tz.BaseUtcOffset.TotalSeconds }, null };
        }
        catch
        {
            return new object?[] { null, new GoError("unknown time zone " + n) };
        }
    }
    private static readonly object UtcLoc = new GoLocation();
}

/// <summary>A time.Time value: Unix nanoseconds in UTC, plus a Go zero-value flag.</summary>
public sealed class GoTime { public long N; public bool IsZero; }

/// <summary>A *time.Location: UTC by default, or a fixed-offset zone from
/// time.FixedZone (Name + OffsetSeconds).</summary>
public sealed class GoLocation { public string Name = "UTC"; public int OffsetSeconds; }

/// <summary>A *time.Ticker / *time.Timer: the C channel plus its driving timer.</summary>
public sealed class GoTicker { public GoChan C = null!; public System.Threading.Timer? Timer; }
