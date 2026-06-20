namespace GoCLR.Stdlib;

using R = System.Text.Rune;

/// <summary>Shim for Go's <c>unicode</c> package (rune classification/mapping).
/// rune is int32. Invalid code points classify as false / map to themselves.</summary>
public static class Unicode
{
    private static bool Ok(int r) => R.IsValid(r);

    public static bool IsDigit(int r) => Ok(r) && R.IsDigit(new R(r));
    public static bool IsNumber(int r) => Ok(r) && R.IsNumber(new R(r));
    public static bool IsLetter(int r) => Ok(r) && R.IsLetter(new R(r));
    public static bool IsSpace(int r) => Ok(r) && R.IsWhiteSpace(new R(r));
    public static bool IsUpper(int r) => Ok(r) && R.IsUpper(new R(r));
    public static bool IsLower(int r) => Ok(r) && R.IsLower(new R(r));
    public static bool IsPunct(int r) => Ok(r) && R.IsPunctuation(new R(r));
    public static bool IsControl(int r) => Ok(r) && R.IsControl(new R(r));
    public static bool IsPrint(int r) => Ok(r) && !R.IsControl(new R(r));
    public static bool IsGraphic(int r) => Ok(r) && (R.IsLetter(new R(r)) || R.IsDigit(new R(r)) || R.IsPunctuation(new R(r)) || R.IsSymbol(new R(r)) || R.IsWhiteSpace(new R(r)));
    public static bool IsLetterOrDigit(int r) => Ok(r) && R.IsLetterOrDigit(new R(r));

    public static int ToUpper(int r) => Ok(r) ? R.ToUpperInvariant(new R(r)).Value : r;
    public static int ToLower(int r) => Ok(r) ? R.ToLowerInvariant(new R(r)).Value : r;
    public static int ToTitle(int r) => ToUpper(r);
}
