// Demo: parse HCL from C# — the source is tokenized, parsed and evaluated by the
// real hashicorp/hcl/v2 library, compiled to .NET with goclr.
using System;
using System.Collections.Generic;
using HclGoClr;

class Demo
{
    static void Main()
    {
        const string src = @"
io_mode  = ""async""
retries  = 3
timeout  = 1.5

service ""http"" ""web_proxy"" {
  listen_addr = ""127.0.0.1:8080""
  enabled     = true
}

service ""https"" ""web_proxy"" {
  listen_addr = ""127.0.0.1:8443""
}
";

        Console.WriteLine("== HCL as JSON ==");
        Console.WriteLine("  " + Hcl.ToJson("config.hcl", src));

        Console.WriteLine("\n== HCL as a .NET object graph ==");
        var cfg = Hcl.Parse("config.hcl", src);
        Show("io_mode", cfg["io_mode"]);
        Show("retries", cfg["retries"]);
        Show("timeout", cfg["timeout"]);

        // service is a list (two blocks), each keyed by protocol then name label.
        var services = (object[])cfg["service"];
        Console.WriteLine($"  services  => {services.Length} block(s)");
        foreach (var s in services)
        {
            var byProto = (Dictionary<string, object>)s;
            foreach (var proto in byProto)
            {
                var byName = (Dictionary<string, object>)proto.Value;
                foreach (var name in byName)
                {
                    var body = (Dictionary<string, object>)name.Value;
                    body.TryGetValue("enabled", out var enabled);
                    Console.WriteLine(
                        $"    {proto.Key,-6} {name.Key,-10} listen_addr={body["listen_addr"]} enabled={enabled ?? "(unset)"}");
                }
            }
        }

        Console.WriteLine("\nAll HCL parsed on the CLR via goclr-compiled hashicorp/hcl/v2.");
    }

    static void Show(string label, object value) =>
        Console.WriteLine($"  {label,-9} => {value} ({value?.GetType().Name ?? "null"})");
}
