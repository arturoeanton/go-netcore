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

    // The wall-clock DateTime in the value's own zone (instant + the zone's fixed offset).
    // OffsetSeconds is 0 for UTC, so UTC values decompose exactly as before.
    private static System.DateTime ToDateTime(GoTime t) => Epoch.AddTicks((t.N + (long)t.OffsetSeconds * Second) / 100);
    private static GoTime FromDateTime(System.DateTime dt) =>
        new GoTime { N = (dt.ToUniversalTime() - Epoch).Ticks * 100, IsZero = false };
    // A new instant carrying the same zone (offset + name) as src.
    private static GoTime With(GoTime src, long n) =>
        new GoTime { N = n, IsZero = false, OffsetSeconds = src.OffsetSeconds, ZoneName = src.ZoneName };

    // Construction.
    public static object Now() => new GoTime { N = (System.DateTime.UtcNow - Epoch).Ticks * 100, IsZero = false };
    public static object Unix(long sec, long nsec) => new GoTime { N = sec * Second + nsec, IsZero = false };
    public static object UnixMilli(long msec) => new GoTime { N = msec * Millisecond, IsZero = false };
    public static object UnixMicro(long usec) => new GoTime { N = usec * Microsecond, IsZero = false };
    public static object Date(long year, long month, long day, long hour, long min, long sec, long nsec, object? loc)
    {
        var dt = new System.DateTime((int)year, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
            .AddMonths((int)month - 1).AddDays(day - 1)
            .AddHours(hour).AddMinutes(min).AddSeconds(sec);
        var t = FromDateTime(dt); t.N += nsec;
        // The y/m/d/h/m/s are wall-clock in loc; the stored instant is that minus the offset.
        if (loc is GoLocation gl)
        {
            t.N -= (long)gl.OffsetSeconds * Second;
            t.OffsetSeconds = gl.OffsetSeconds;
            t.ZoneName = gl.Name;
        }
        return t;
    }
    public static long Since(object t) => (System.DateTime.UtcNow - Epoch).Ticks * 100 - ((GoTime)t).N;
    public static long Until(object t) => ((GoTime)t).N - (System.DateTime.UtcNow - Epoch).Ticks * 100;

    // Methods (receiver passed as first arg).
    public static long Time_Unix(object t) => ((GoTime)t).N / Second;
    public static long Time_UnixNano(object t) => ((GoTime)t).N;
    public static long Time_UnixMilli(object t) => ((GoTime)t).N / Millisecond;
    public static long Time_Year(object t) => ZeroDate(t, dt => dt.Year, 1);
    public static long Time_YearDay(object t) => ZeroDate(t, dt => dt.DayOfYear, 1);
    // (t Time).Date() (year int, month Month, day int) and Clock() (hour, min, sec int).
    public static object?[] Time_Date(object t) =>
        new object?[] { Time_Year(t), Time_Month(t), Time_Day(t) };
    public static object?[] Time_Clock(object t) =>
        new object?[] { Time_Hour(t), Time_Minute(t), Time_Second(t) };
    // The value's fixed zone (name, offset in seconds east of UTC).
    public static object?[] Time_Zone(object t) { var gt = (GoTime)t; return new object?[] { GoString.FromDotNetString(gt.ZoneName), (long)gt.OffsetSeconds }; }
    // (t Time).In(loc): the same instant displayed in loc's zone.
    public static object Time_In(object t, object loc)
    {
        var gt = (GoTime)t; var gl = loc as GoLocation;
        return new GoTime { N = gt.N, IsZero = gt.IsZero, OffsetSeconds = gl?.OffsetSeconds ?? 0, ZoneName = gl?.Name ?? "UTC" };
    }
    public static object Time_Location(object t) { var gt = (GoTime)t; return new GoLocation { Name = gt.ZoneName, OffsetSeconds = gt.OffsetSeconds }; }
    public static long Time_Month(object t) => ZeroDate(t, dt => dt.Month, 1);
    public static long Time_Day(object t) => ZeroDate(t, dt => dt.Day, 1);
    public static long Time_Hour(object t) => ZeroDate(t, dt => dt.Hour, 0);
    public static long Time_Minute(object t) => ZeroDate(t, dt => dt.Minute, 0);
    public static long Time_Second(object t) => ZeroDate(t, dt => dt.Second, 0);
    public static long Time_Nanosecond(object t) => ((GoTime)t).N % Second;
    public static long Time_Weekday(object t) => ZeroDate(t, dt => (int)dt.DayOfWeek, 1);
    public static object Time_Add(object t, long d) => With((GoTime)t, ((GoTime)t).N + d);
    // (t Time).AddDate(years, months, days int) Time — calendar arithmetic.
    public static object Time_AddDate(object t, long years, long months, long days)
    {
        // Go adds the calendar components then NORMALIZES via time.Date: an out-of-range
        // result (Jan 31 + 1 month = Feb 31) rolls over (-> Mar 3), it does NOT clamp to the
        // month's last day the way .NET's AddMonths does. Route through Date(), whose AddDays
        // path normalizes the same way Go does (and preserves the full sub-second nanos).
        var gt = (GoTime)t;
        var dt = ToDateTime(gt);
        long nanos = ((gt.N % Second) + Second) % Second;
        // Re-apply the value's own zone so AddDate preserves the location (Date with a matching
        // GoLocation interprets the wall-clock fields in that zone).
        return Date(dt.Year + years, dt.Month + months, dt.Day + days,
                    dt.Hour, dt.Minute, dt.Second, nanos,
                    new GoLocation { Name = gt.ZoneName, OffsetSeconds = gt.OffsetSeconds });
    }
    public static object Time_Round(object t, long d) { var gt = (GoTime)t; var n = gt.N; if (d <= 0) return gt.IsZero ? t : With(gt, n); long r = n % d; n = r + r < d ? n - r : n - r + d; return With(gt, n); }
    public static object Time_Truncate(object t, long d) { var gt = (GoTime)t; var n = gt.N; if (d <= 0) return gt.IsZero ? t : With(gt, n); return With(gt, n - n % d); }
    public static long Time_Sub(object t, object u) => ((GoTime)t).N - ((GoTime)u).N;
    public static bool Time_Before(object t, object u) => ((GoTime)t).N < ((GoTime)u).N;
    public static bool Time_After(object t, object u) => ((GoTime)t).N > ((GoTime)u).N;
    public static bool Time_Equal(object t, object u) => ((GoTime)t).N == ((GoTime)u).N;
    public static bool Time_IsZero(object t) => ((GoTime)t).IsZero;
    public static object Time_UTC(object t) { var gt = (GoTime)t; return new GoTime { N = gt.N, IsZero = gt.IsZero }; } // same instant, UTC zone
    public static object Time_Local(object t) { var gt = (GoTime)t; return new GoTime { N = gt.N, IsZero = gt.IsZero }; } // Local == UTC in goclr
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

    // ---- additional time methods ----
    private const string RFC3339NanoLayout = "2006-01-02T15:04:05.999999999Z07:00";
    private const long UnixToInternalSec = 62135596800L; // seconds from Jan 1, year 1 to the Unix epoch
    private static readonly string[] MonthNames =
        { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

    private static GoSlice BytesOf(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static GoSlice AppendBytes(GoSlice b, byte[] extra)
    {
        var d = new object?[b.Len + extra.Length];
        for (int i = 0; i < b.Len; i++) d[i] = b.Data![b.Off + i];
        for (int i = 0; i < extra.Length; i++) d[b.Len + i] = (int)extra[i];
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    // (time.Duration).Abs()
    public static long Duration_Abs(long d) => d >= 0 ? d : (d == long.MinValue ? long.MaxValue : -d);

    // (time.Location).String()
    public static GoString Location_String(object loc) => GoString.FromDotNetString(((GoLocation)loc).Name);

    // (time.Time).Compare(u) -1/0/1
    public static long Time_Compare(object t, object u)
    {
        long a = ((GoTime)t).N, b = ((GoTime)u).N;
        return a < b ? -1L : (a > b ? 1L : 0L);
    }

    // (time.Time).UnixMicro()
    public static long Time_UnixMicro(object t) => ((GoTime)t).N / Microsecond;

    // (time.Time).ISOWeek() (year, week int)
    public static object?[] Time_ISOWeek(object t)
    {
        var dt = ToDateTime((GoTime)t);
        return new object?[] { (long)System.Globalization.ISOWeek.GetYear(dt), (long)System.Globalization.ISOWeek.GetWeekOfYear(dt) };
    }

    // (time.Time).IsDST() — UTC/fixed-offset zones never observe DST in this model.
    public static bool Time_IsDST(object t) => false;

    // (time.Time).GoString() — Go's %#v spelling.
    public static GoString Time_GoString(object t)
    {
        var g = (GoTime)t;
        var dt = ToDateTime(g);
        long ns = g.N % Second; if (ns < 0) ns += Second;
        return GoString.FromDotNetString(
            $"time.Date({dt.Year}, time.{MonthNames[dt.Month - 1]}, {dt.Day}, {dt.Hour}, {dt.Minute}, {dt.Second}, {ns}, time.UTC)");
    }

    // Text marshaling (RFC 3339 with nanoseconds).
    public static object?[] Time_MarshalText(object t) =>
        new object?[] { BytesOf(System.Text.Encoding.UTF8.GetBytes(DoFormat((GoTime)t, RFC3339NanoLayout))), null };
    public static object?[] Time_AppendText(object t, GoSlice b) =>
        new object?[] { AppendBytes(b, System.Text.Encoding.UTF8.GetBytes(DoFormat((GoTime)t, RFC3339NanoLayout))), null };
    public static object?[] Time_MarshalJSON(object t) =>
        new object?[] { BytesOf(System.Text.Encoding.UTF8.GetBytes("\"" + DoFormat((GoTime)t, RFC3339NanoLayout) + "\"")), null };

    // Helpers for the json shim to treat a time.Time field/value like Go's Time.MarshalJSON /
    // UnmarshalJSON (an RFC3339 quoted string) instead of the raw GoTime struct fields.
    public static string JsonText(object t) => "\"" + DoFormat((GoTime)t, RFC3339NanoLayout) + "\"";
    public static object ParseRFC3339(string s)
    {
        var r = Parse(GoString.FromDotNetString(RFC3339NanoLayout), GoString.FromDotNetString(s));
        return r[1] == null ? r[0]! : TimeZero();
    }

    // Binary marshaling — faithful port of Go's V1 layout (UTC ⇒ offsetMin = -1):
    // [version=1][sec int64 BE][nsec int32 BE][offsetMin int16 BE] = 15 bytes. GobEncode == MarshalBinary.
    private static byte[] MarshalBin(GoTime g)
    {
        long n = g.N;
        long sec = n / Second, nsec = n % Second;
        if (nsec < 0) { nsec += Second; sec--; }
        long absSec = sec + UnixToInternalSec;
        int ns = (int)nsec;
        return new byte[]
        {
            1,
            (byte)(absSec >> 56), (byte)(absSec >> 48), (byte)(absSec >> 40), (byte)(absSec >> 32),
            (byte)(absSec >> 24), (byte)(absSec >> 16), (byte)(absSec >> 8), (byte)absSec,
            (byte)(ns >> 24), (byte)(ns >> 16), (byte)(ns >> 8), (byte)ns,
            0xff, 0xff, // offsetMin = -1 (UTC)
        };
    }
    public static object?[] Time_MarshalBinary(object t) => new object?[] { BytesOf(MarshalBin((GoTime)t)), null };
    public static object?[] Time_GobEncode(object t) => new object?[] { BytesOf(MarshalBin((GoTime)t)), null };
    public static object?[] Time_AppendBinary(object t, GoSlice b) => new object?[] { AppendBytes(b, MarshalBin((GoTime)t)), null };

    // The Unmarshal/GobDecode receivers are *Time; resolve the underlying GoTime to mutate it.
    private static GoTime RecvTime(object t) => t is GoPtr p ? (GoTime)GoPtrs.Get(p)! : (GoTime)t;
    private static string BytesToStr(GoSlice b)
    {
        var by = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) by[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return System.Text.Encoding.UTF8.GetString(by);
    }
    private static object? ParseInto(object t, string s)
    {
        var r = Parse(GoString.FromDotNetString(RFC3339NanoLayout), GoString.FromDotNetString(s));
        if (r[1] != null) return r[1];
        var parsed = (GoTime)r[0]!;
        var g = RecvTime(t); g.N = parsed.N; g.IsZero = parsed.IsZero;
        return null;
    }
    public static object? Time_UnmarshalText(object t, GoSlice data) => ParseInto(t, BytesToStr(data));
    public static object? Time_UnmarshalJSON(object t, GoSlice data)
    {
        string s = BytesToStr(data);
        if (s == "null") return null;                       // Go leaves the time unchanged for JSON null
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"') s = s.Substring(1, s.Length - 2);
        return ParseInto(t, s);
    }
    public static object? Time_UnmarshalBinary(object t, GoSlice data)
    {
        var buf = new byte[data.Len];
        for (int i = 0; i < data.Len; i++) buf[i] = (byte)System.Convert.ToInt64(data.Data![data.Off + i]);
        if (buf.Length == 0) return new GoError(GoString.FromDotNetString("Time.UnmarshalBinary: no data"));
        byte version = buf[0];
        if (version != 1 && version != 2) return new GoError(GoString.FromDotNetString("Time.UnmarshalBinary: unsupported version"));
        int wantLen = version == 1 ? 15 : 16;
        if (buf.Length != wantLen) return new GoError(GoString.FromDotNetString("Time.UnmarshalBinary: invalid length"));
        long sec = ((long)buf[1] << 56) | ((long)buf[2] << 48) | ((long)buf[3] << 40) | ((long)buf[4] << 32)
                 | ((long)buf[5] << 24) | ((long)buf[6] << 16) | ((long)buf[7] << 8) | buf[8];
        long nsec = (buf[9] << 24) | (buf[10] << 16) | (buf[11] << 8) | buf[12];
        long n = (sec - UnixToInternalSec) * Second + nsec;
        var g = RecvTime(t); g.N = n; g.IsZero = false;
        return null;
    }
    public static object? Time_GobDecode(object t, GoSlice data) => Time_UnmarshalBinary(t, data);

    // (time.Time).ZoneBounds() — UTC has no transitions, so both bounds are the zero Time.
    public static object?[] Time_ZoneBounds(object t) => new object?[] { new GoTime { IsZero = true }, new GoTime { IsZero = true } };

    // time.Parse(layout, value) (Time, error): the inverse of Format — walk the Go
    // reference-time layout, consuming the matching run from value for each token.
    // Returns the zero Time and an error if value does not match the layout. The
    // result is in UTC (goclr's time is UTC-only; see docs/LIMITATIONS.md).
    public static object?[] Parse(GoString layout, GoString value) => ParseImpl(layout, value, null);

    // Thrown when a parsed time field is outside its valid range (Go's "<field> out of range").
    private sealed class RangeError : System.Exception { public readonly string Field; public RangeError(string f) { Field = f; } }
    // Thrown when input remains after the layout is fully consumed (Go's "extra text: ...").
    private sealed class ExtraTextError : System.Exception { public readonly string Rest; public ExtraTextError(string r) { Rest = r; } }

    private static object?[] ParseImpl(GoString layout, GoString value, GoLocation? defaultLoc)
    {
        string lay = layout.ToDotNetString(), val = value.ToDotNetString();
        // Go defaults a missing year to 0; goclr's GoTime counts nanoseconds from the
        // Unix epoch (representable range ~1678..2262), so a yearless layout uses 1970
        // to keep the parsed clock fields exact without overflowing (see docs/LIMITATIONS.md).
        int year = 1970, month = 1, day = 1, hour = 0, min = 0, sec = 0, nsec = 0;
        bool hasPM = false, pm = false, hour12 = false;
        int li = 0, vi = 0;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        bool Tok(string tok) =>
            li + tok.Length <= lay.Length && string.CompareOrdinal(lay, li, tok, 0, tok.Length) == 0 && (li += tok.Length) >= 0;
        // Read up to `max` digits (at least one). When `exact`, require exactly `max` digits —
        // Go's zero-padded tokens (01/02/03/04/05/06/2006) use getnum(value, true), so a single
        // digit before a non-digit fails there; the non-padded forms (1/2/3/4/5/15/_2) accept 1.
        int ReadInt(int max, bool exact = false)
        {
            if (!exact && vi < val.Length && val[vi] == ' ') vi++; // tolerate space padding (_2)
            int start = vi, n = 0;
            while (vi < val.Length && n < max && char.IsDigit(val[vi])) { vi++; n++; }
            if (vi == start || (exact && n != max)) throw new System.FormatException();
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
            if (vi < val.Length && (val[vi] == '.' || val[vi] == ','))
            {
                vi++;
                int s = vi;
                while (vi < val.Length && char.IsDigit(val[vi])) vi++;
                string d = val.Substring(s, vi - s);
                if (d.Length > 9) d = d.Substring(0, 9);
                if (d.Length > 0) nsec = int.Parse(d.PadRight(9, '0'), inv);
            }
        }
        int? zoneOff = null;
        void ReadZone()
        {
            if (vi < val.Length && val[vi] == 'Z') { vi++; zoneOff = 0; return; }
            if (vi < val.Length && (val[vi] == '+' || val[vi] == '-'))
            {
                int sign = val[vi] == '-' ? -1 : 1; vi++;
                int hh = ReadInt(2);
                if (vi < val.Length && val[vi] == ':') vi++;
                int mm = (vi < val.Length && char.IsDigit(val[vi])) ? ReadInt(2) : 0;
                zoneOff = sign * (hh * 3600 + mm * 60);
            }
        }

        int liStart = 0, viStart = 0; // start of the layout element / value position being parsed
        try
        {
            while (li < lay.Length)
            {
                liStart = li; viStart = vi;
                // Numeric fields are range-checked inline (as Go validates each field while
                // parsing, before a later literal can fail): a bad value reports "<field> out
                // of range" rather than "cannot parse".
                if (Tok("2006")) year = ReadInt(4, true);
                else if (Tok("06")) year = 2000 + ReadInt(2, true);
                else if (Tok("January")) month = ReadName(MonthsLong) + 1;
                else if (Tok("Jan")) month = ReadName(MonthsAbbr) + 1;
                else if (Tok("01")) { month = ReadInt(2, true); if (month < 1 || month > 12) throw new RangeError("month"); }
                else if (Tok("Monday")) ReadName(DaysLong);
                else if (Tok("Mon")) ReadName(DaysAbbr);
                else if (Tok("02")) { day = ReadInt(2, true); if (day < 1 || day > 31) throw new RangeError("day"); }
                else if (Tok("_2")) { day = ReadInt(2); if (day < 1 || day > 31) throw new RangeError("day"); }
                else if (Tok("15")) { hour = ReadInt(2); if (hour < 0 || hour > 23) throw new RangeError("hour"); }
                else if (Tok("03")) { hour = ReadInt(2, true); hour12 = true; if (hour < 0 || hour > 12) throw new RangeError("hour"); }
                else if (Tok("04")) { min = ReadInt(2, true); if (min < 0 || min > 59) throw new RangeError("minute"); }
                else if (Tok("05")) { sec = ReadInt(2, true); if (sec < 0 || sec > 60) throw new RangeError("second"); }
                else if (Tok("PM") || Tok("pm"))
                {
                    hasPM = true;
                    if (vi + 2 <= val.Length) { pm = val.Substring(vi, 2).ToUpperInvariant() == "PM"; vi += 2; }
                }
                else if (li < lay.Length && (lay[li] == '.' || lay[li] == ',') &&
                         li + 1 < lay.Length && (lay[li + 1] == '0' || lay[li + 1] == '9'))
                {
                    // fractional-second layout token: separator + run of '0'/'9' (any width).
                    char kind = lay[li + 1];
                    li++;
                    while (li < lay.Length && lay[li] == kind) li++;
                    ReadFrac();
                }
                else if (Tok("Z07:00") || Tok("Z0700") || Tok("Z07")) ReadZone();
                else if (Tok("-07:00") || Tok("-0700") || Tok("-07")) ReadZone();
                else if (Tok("MST")) { while (vi < val.Length && char.IsLetter(val[vi])) vi++; }
                else if (Tok("3")) { hour = ReadInt(2); hour12 = true; if (hour < 0 || hour > 12) throw new RangeError("hour"); }
                else if (Tok("2")) { day = ReadInt(2); if (day < 1 || day > 31) throw new RangeError("day"); }
                else if (Tok("1")) { month = ReadInt(2); if (month < 1 || month > 12) throw new RangeError("month"); }
                else if (Tok("4")) { min = ReadInt(2); if (min < 0 || min > 59) throw new RangeError("minute"); }
                else if (Tok("5")) { sec = ReadInt(2); if (sec < 0 || sec > 60) throw new RangeError("second"); }
                else
                {
                    if (vi < val.Length && val[vi] == lay[li]) { vi++; li++; }
                    else throw new System.FormatException();
                }
            }
            // Go reports unconsumed input after the layout is exhausted.
            if (vi < val.Length) throw new ExtraTextError(val.Substring(vi));
            if (hasPM) { if (pm && hour < 12) hour += 12; else if (!pm && hour == 12) hour = 0; }
            var dt = new System.DateTime(year, month, day, hour, min, sec, System.DateTimeKind.Utc);
            var t = FromDateTime(dt);
            t.N += nsec;
            if (zoneOff.HasValue)
            {
                // The parsed clock is wall-clock at the input's offset; store the UTC instant
                // and (for a non-zero offset) carry the zone.
                t.N -= (long)zoneOff.Value * Second;
                if (zoneOff.Value != 0) { t.OffsetSeconds = zoneOff.Value; t.ZoneName = ZoneOffset(zoneOff.Value, false); }
            }
            else if (defaultLoc != null)
            {
                // ParseInLocation with no zone in the layout: interpret in loc.
                t.N -= (long)defaultLoc.OffsetSeconds * Second;
                t.OffsetSeconds = defaultLoc.OffsetSeconds;
                t.ZoneName = defaultLoc.Name;
            }
            return new object?[] { t, null };
        }
        catch (RangeError re)
        {
            // A parsed field was out of range: Go reports `parsing time "VALUE": <field> out of range`.
            return new object?[] { new GoTime { IsZero = true },
                new GoError("parsing time \"" + val + "\": " + re.Field + " out of range") };
        }
        catch (ExtraTextError et)
        {
            return new object?[] { new GoTime { IsZero = true },
                new GoError("parsing time \"" + val + "\": extra text: \"" + et.Rest + "\"") };
        }
        catch
        {
            // Go's message names where it stopped: the unparsed value remainder and the layout
            // element it expected there (the matched token, e.g. "2006"; for a literal mismatch,
            // the remaining layout text).
            string remaining = val.Substring(System.Math.Min(viStart, val.Length));
            string std = li > liStart ? lay.Substring(liStart, li - liStart) : lay.Substring(liStart);
            return new object?[] { new GoTime { IsZero = true },
                new GoError("parsing time \"" + val + "\" as \"" + lay + "\": cannot parse \"" + remaining + "\" as \"" + std + "\"") };
        }
    }

    private static long ZeroDate(object t, System.Func<System.DateTime, int> f, int zero)
        => ((GoTime)t).IsZero ? zero : f(ToDateTime((GoTime)t));

    private static readonly string[] MonthsLong = { "January","February","March","April","May","June","July","August","September","October","November","December" };
    private static readonly string[] MonthsAbbr = { "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };
    private static readonly string[] DaysLong = { "Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday" };
    private static readonly string[] DaysAbbr = { "Sun","Mon","Tue","Wed","Thu","Fri","Sat" };

    // A numeric zone offset like "-0500" / "-05:00" from seconds east of UTC.
    private static string ZoneOffset(int offsetSec, bool colon)
    {
        char sign = offsetSec < 0 ? '-' : '+';
        int a = System.Math.Abs(offsetSec), hh = a / 3600, mm = (a % 3600) / 60;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return colon ? $"{sign}{hh.ToString("D2", inv)}:{mm.ToString("D2", inv)}"
                     : $"{sign}{hh.ToString("D2", inv)}{mm.ToString("D2", inv)}";
    }

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
            else if ((layout[i] == '.' || layout[i] == ',') && i + 1 < layout.Length && (layout[i + 1] == '0' || layout[i + 1] == '9'))
            {
                // Fractional seconds: separator ('.' or ',') followed by a run of '0' or '9'.
                // '0' → fixed width (keep trailing zeros); '9' → trim trailing zeros (omit
                // separator entirely when the result is empty). Matches Go's stdFracSecond.
                char sep = layout[i];
                char kind = layout[i + 1];
                int j = i + 1;
                while (j < layout.Length && layout[j] == kind) j++;
                int n = j - i - 1;
                i = j;
                string nine = nanos.ToString("D9", inv);
                string frac = n <= 9 ? nine.Substring(0, n) : nine + new string('0', n - 9);
                if (kind == '9')
                {
                    frac = frac.TrimEnd('0');
                    if (frac.Length > 0) sb.Append(sep).Append(frac);
                }
                else { sb.Append(sep).Append(frac); }
            }
            else if (M("Z07:00")) { sb.Append(t.OffsetSeconds == 0 ? "Z" : ZoneOffset(t.OffsetSeconds, true)); }
            else if (M("Z0700")) { sb.Append(t.OffsetSeconds == 0 ? "Z" : ZoneOffset(t.OffsetSeconds, false)); }
            else if (M("-07:00")) { sb.Append(ZoneOffset(t.OffsetSeconds, true)); }
            else if (M("-0700")) { sb.Append(ZoneOffset(t.OffsetSeconds, false)); }
            else if (M("MST")) { sb.Append(t.ZoneName.Length > 0 ? t.ZoneName : "UTC"); }
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
    public static object?[] ParseInLocation(GoString layout, GoString value, object loc) => ParseImpl(layout, value, loc as GoLocation);

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

/// <summary>A time.Time value: the instant as Unix nanoseconds (UTC), plus the location's
/// fixed UTC offset and zone name for wall-clock display. OffsetSeconds 0 / "UTC" is the
/// default, so a UTC time behaves exactly as before.</summary>
[GoShim("time.Time")]
public sealed class GoTime { public long N; public bool IsZero; public int OffsetSeconds; public string ZoneName = "UTC"; }

/// <summary>A *time.Location: UTC by default, or a fixed-offset zone from
/// time.FixedZone (Name + OffsetSeconds).</summary>
[GoShim("time.Location")]
public sealed class GoLocation { public string Name = "UTC"; public int OffsetSeconds; }

/// <summary>A *time.Ticker / *time.Timer: the C channel plus its driving timer.</summary>
public sealed class GoTicker { public GoChan C = null!; public System.Threading.Timer? Timer; }
