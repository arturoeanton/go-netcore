namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>unicode/utf16</c> (runes are int32; []uint16 is a
/// GoSlice of boxed uint values).</summary>
public static class Utf16
{
    private const int replacementChar = 0xFFFD;
    private const int surr1 = 0xd800, surr2 = 0xdc00, surr3 = 0xe000;
    private const int surrSelf = 0x10000, maxRune = 0x10FFFF;

    public static bool IsSurrogate(int r) => surr1 <= r && r < surr3;

    public static object?[] EncodeRune(int r)
    {
        if (r < surrSelf || r > maxRune)
            return new object?[] { replacementChar, replacementChar };
        r -= surrSelf;
        return new object?[] { surr1 + ((r >> 10) & 0x3ff), surr2 + (r & 0x3ff) };
    }

    public static int DecodeRune(int r1, int r2)
    {
        if (surr1 <= r1 && r1 < surr2 && surr2 <= r2 && r2 < surr3)
            return ((r1 - surr1) << 10) | (r2 - surr2) + surrSelf;
        return replacementChar;
    }

    private static int U16(object? v) => (int)(System.Convert.ToInt64(v) & 0xffff);

    public static GoSlice Encode(GoSlice runes)
    {
        var outp = new System.Collections.Generic.List<object?>();
        for (int i = 0; i < runes.Len; i++)
        {
            int r = (int)System.Convert.ToInt64(runes.Data![runes.Off + i]);
            if (0 <= r && r < surrSelf)
            {
                if (surr1 <= r && r < surr3) outp.Add((uint)replacementChar);
                else outp.Add((uint)r);
            }
            else if (surrSelf <= r && r <= maxRune)
            {
                int rr = r - surrSelf;
                outp.Add((uint)(surr1 + ((rr >> 10) & 0x3ff)));
                outp.Add((uint)(surr2 + (rr & 0x3ff)));
            }
            else outp.Add((uint)replacementChar);
        }
        return new GoSlice { Data = outp.ToArray(), Off = 0, Len = outp.Count, Cap = outp.Count };
    }

    public static GoSlice Decode(GoSlice s)
    {
        var outp = new System.Collections.Generic.List<object?>();
        for (int i = 0; i < s.Len; i++)
        {
            int r = U16(s.Data![s.Off + i]);
            if (surr1 <= r && r < surr2 && i + 1 < s.Len)
            {
                int r2 = U16(s.Data[s.Off + i + 1]);
                if (surr2 <= r2 && r2 < surr3)
                {
                    outp.Add(((r - surr1) << 10) | (r2 - surr2) + surrSelf);
                    i++;
                    continue;
                }
                outp.Add(replacementChar);
            }
            else if (surr1 <= r && r < surr3) outp.Add(replacementChar);
            else outp.Add(r);
        }
        return new GoSlice { Data = outp.ToArray(), Off = 0, Len = outp.Count, Cap = outp.Count };
    }
}
