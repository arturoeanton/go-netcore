namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An html/template.Template handle. goclr does not render Go templates, so
/// this models the builder API as no-ops (parsing loads nothing, Execute writes
/// nothing); a server that does not render HTML templates is unaffected.</summary>
public sealed class GoTemplate { public string Name = ""; }

/// <summary>Shim for html/template's builder surface used by gin. Template execution
/// is not implemented — Execute/ExecuteTemplate write nothing and report success.</summary>
public static class Template
{
    public static object New(GoString name) => new GoTemplate { Name = name.ToDotNetString() };

    // template.Must(t, err): panics on a non-nil error, else returns t.
    public static object? Must(object? t, object? err)
    {
        if (err != null) throw new GoPanicException(((IGoError)err).Error());
        return t;
    }

    public static GoString JSEscapeString(GoString s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.ToDotNetString())
            sb.Append(c switch { '<' => "\\u003c", '>' => "\\u003e", '&' => "\\u0026", '\'' => "\\u0027", '"' => "\\u0022", _ => c.ToString() });
        return GoString.FromDotNetString(sb.ToString());
    }

    // (*Template) builder methods — each returns the template so calls chain.
    public static object Tmpl_New(object t, GoString name) => t;
    public static object Tmpl_Delims(object t, GoString left, GoString right) => t;
    public static object Tmpl_Funcs(object t, GoMap funcMap) => t;
    public static object?[] Tmpl_Parse(object t, GoString text) => new object?[] { t, null };
    public static object?[] Tmpl_ParseFiles(object t, GoSlice files) => new object?[] { t, null };
    public static object?[] Tmpl_ParseGlob(object t, GoString glob) => new object?[] { t, null };
    public static object? Tmpl_Execute(object t, object? w, object? data) => null;
    public static object? Tmpl_ExecuteTemplate(object t, object? w, GoString name, object? data) => null;
    // Templates(): goclr loads no templates, so the set is just this template.
    public static GoSlice Tmpl_Templates(object t) => new() { Data = new object?[] { t }, Off = 0, Len = 1, Cap = 1 };
    public static GoString Tmpl_Name(object t) => GoString.FromDotNetString(t is GoTemplate g ? g.Name : "");
    public static object Tmpl_Lookup(object t, GoString name) => t;
    public static object Tmpl_Option(object t, GoSlice opts) => t;
}
