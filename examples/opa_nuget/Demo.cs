// Demo: evaluate a Rego authorization policy from C# — the module is parsed,
// compiled and evaluated by the real open-policy-agent/opa engine, compiled to .NET
// with goclr.
using System;
using OpaGoClr;

class Demo
{
    static void Main()
    {
        const string module = @"
package example

default allow = false

allow {
	input.role == ""admin""
}

allow {
	input.role == ""user""
	input.action == ""read""
}
";

        var opa = new OpaPolicy(module);

        Console.WriteLine("== Rego authorization policy (data.example.allow) ==");
        var requests = new[]
        {
            new { role = "admin", action = "delete" },
            new { role = "user",  action = "read"   },
            new { role = "user",  action = "write"  },
        };
        foreach (var req in requests)
        {
            bool allow = opa.EvalBool("data.example.allow", req);
            Console.WriteLine($"  role={req.role,-6} action={req.action,-7} => allow={allow}");
        }

        Console.WriteLine("\n== Raw OPA result set as JSON ==");
        Console.WriteLine("  " + opa.Eval("data.example.allow", new { role = "admin" }));

        Console.WriteLine("\n== A policy that computes a value, not just a bool ==");
        const string tiers = @"
package billing

tier = ""enterprise"" { input.seats >= 100 }
tier = ""team""       { input.seats >= 10; input.seats < 100 }
tier = ""solo""       { input.seats < 10 }
";
        var billing = new OpaPolicy(tiers);
        foreach (var seats in new[] { 3, 42, 500 })
        {
            using var v = billing.EvalValue("data.billing.tier", new { seats });
            Console.WriteLine($"  seats={seats,-4} => tier={v.Value.GetString()}");
        }

        Console.WriteLine("\nAll Rego evaluated on the CLR via goclr-compiled open-policy-agent/opa.");
    }
}
