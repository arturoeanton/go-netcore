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
}
