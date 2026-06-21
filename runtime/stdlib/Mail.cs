namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A net/mail.Address (parsed name + address).</summary>
public sealed class GoMailAddress { public string Name = ""; public string Address = ""; }

/// <summary>Shim for a subset of Go's <c>net/mail</c> (address parsing).</summary>
public static class Mail
{
    // mail.ParseAddress(address) (*Address, error).
    public static object?[] ParseAddress(GoString s)
    {
        try
        {
            var m = new System.Net.Mail.MailAddress(s.ToDotNetString());
            return new object?[] { new GoMailAddress { Name = m.DisplayName, Address = m.Address }, null };
        }
        catch
        {
            return new object?[] { null, new GoError("mail: invalid address: " + s.ToDotNetString()) };
        }
    }

    public static GoString Address_Name(object a) => GoString.FromDotNetString(((GoMailAddress)a).Name);
    public static GoString Address_Address(object a) => GoString.FromDotNetString(((GoMailAddress)a).Address);
}
