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

    // time.Duration methods (receiver is the int64 nanosecond count).
    public static double Duration_Seconds(long d) => (double)d / Second;
    public static double Duration_Minutes(long d) => (double)d / Minute;
    public static double Duration_Hours(long d) => (double)d / Hour;
    public static long Duration_Nanoseconds(long d) => d;
    public static long Duration_Microseconds(long d) => d / Microsecond;
    public static long Duration_Milliseconds(long d) => d / Millisecond;

    public static GoString Duration_String(long d)
    {
        if (d == 0) return GoString.FromDotNetString("0s");
        var sb = new System.Text.StringBuilder();
        long n = d;
        if (n < 0) { sb.Append('-'); n = -n; }
        if (n >= Second)
        {
            double secs = (double)n / Second;
            long h = (long)(secs / 3600); secs -= h * 3600;
            long m = (long)(secs / 60); secs -= m * 60;
            if (h > 0) sb.Append(h).Append('h');
            if (h > 0 || m > 0) sb.Append(m).Append('m');
            sb.Append(secs.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture)).Append('s');
        }
        else if (n >= Millisecond) sb.Append(((double)n / Millisecond).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).Append("ms");
        else if (n >= Microsecond) sb.Append(((double)n / Microsecond).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append("µs");
        else sb.Append(n).Append("ns");
        return GoString.FromDotNetString(sb.ToString());
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
    private static readonly object UtcLoc = new GoLocation();
}

/// <summary>A time.Time value: Unix nanoseconds in UTC, plus a Go zero-value flag.</summary>
public sealed class GoTime { public long N; public bool IsZero; }

/// <summary>Placeholder for a *time.Location (treated as UTC).</summary>
public sealed class GoLocation { }
