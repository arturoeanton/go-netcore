// Demo: use Google CEL from C# — the expressions are parsed, type-checked and
// evaluated by the real cel-go library, compiled to .NET with goclr.
using System;
using CelGoClr;

class Demo
{
    static void Main()
    {
        // 1. An authorization rule, compiled once and evaluated for different subjects.
        var authz = Cel.Compile(
            "user.role in ['admin', 'moderator'] || user.id == doc.owner",
            "user", "doc");

        Check(authz, "admin edits anything",
            new { user = new { role = "admin", id = 1 }, doc = new { owner = 99 } }, true);
        Check(authz, "owner edits own doc",
            new { user = new { role = "guest", id = 7 }, doc = new { owner = 7 } }, true);
        Check(authz, "guest edits other's doc",
            new { user = new { role = "guest", id = 7 }, doc = new { owner = 99 } }, false);

        // 2. Arithmetic + string + collection expressions.
        Show("1 + 2 * 3", Cel.Compile("1 + 2 * 3").Evaluate(null));
        Show("'saas'.size()", Cel.Compile("'saas'.size()").Evaluate(null));
        Show("[1,2,3,4].filter(x, x % 2 == 0)", Cel.Compile("[1, 2, 3, 4].filter(x, x % 2 == 0)").Evaluate(null));
        Show("age >= 21 ? 'adult' : 'minor'",
            Cel.Compile("age >= 21 ? 'adult' : 'minor'", "age").Evaluate(new { age = 30 }));

        // 3. A feature-flag style rule.
        var flag = Cel.Compile("region == 'us' && plan in ['pro', 'enterprise']", "region", "plan");
        Show("flag(us, pro)", flag.EvaluateBool(new { region = "us", plan = "pro" }));
        Show("flag(eu, pro)", flag.EvaluateBool(new { region = "eu", plan = "pro" }));

        Console.WriteLine("\nAll CEL expressions evaluated on the CLR via goclr-compiled cel-go.");
    }

    static void Check(CelProgram p, string label, object vars, bool expected)
    {
        bool got = p.EvaluateBool(vars);
        Console.WriteLine($"  [{(got == expected ? "OK" : "!!")}] {label,-28} => {got}");
    }

    static void Show(string label, object value)
    {
        if (value is object[] arr) value = "[" + string.Join(", ", arr) + "]";
        Console.WriteLine($"  {label,-40} => {value}");
    }
}
