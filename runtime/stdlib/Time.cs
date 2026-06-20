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
    public static long Time_Sub(object t, object u) => ((GoTime)t).N - ((GoTime)u).N;
    public static bool Time_Before(object t, object u) => ((GoTime)t).N < ((GoTime)u).N;
    public static bool Time_After(object t, object u) => ((GoTime)t).N > ((GoTime)u).N;
    public static bool Time_Equal(object t, object u) => ((GoTime)t).N == ((GoTime)u).N;
    public static bool Time_IsZero(object t) => ((GoTime)t).IsZero;
    public static object Time_UTC(object t) => t;
    public static object Time_Local(object t) => t;
    public static GoString Time_String(object t) => GoString.FromDotNetString(DoFormat((GoTime)t, "2006-01-02 15:04:05.999999999 -0700 MST"));
    public static GoString Time_Format(object t, GoString layout) => GoString.FromDotNetString(DoFormat((GoTime)t, layout.ToDotNetString()));

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
    private static readonly object UtcLoc = new GoLocation();
}

/// <summary>A time.Time value: Unix nanoseconds in UTC, plus a Go zero-value flag.</summary>
public sealed class GoTime { public long N; public bool IsZero; }

/// <summary>A *time.Location: UTC by default, or a fixed-offset zone from
/// time.FixedZone (Name + OffsetSeconds).</summary>
public sealed class GoLocation { public string Name = "UTC"; public int OffsetSeconds; }
