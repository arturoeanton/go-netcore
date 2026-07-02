// Hcl.GoCLR — a C# facade over HashiCorp's HCL (HashiCorp Configuration Language),
// implemented by compiling the production Go hashicorp/hcl/v2 library to a .NET
// assembly with goclr. No sidecar, no P/Invoke: the HCL parser runs in-process on
// the CLR.
//
//   var cfg = Hcl.Parse("config.hcl", File.ReadAllText("config.hcl"));
//   string mode = (string)cfg["io_mode"];
//
//   // or get the raw JSON:
//   string json = Hcl.ToJson("config.hcl", src);
//
// The bridge is string-only (GoString ↔ System.String); HCL crosses to C# as JSON,
// so no hcl/cty type ever surfaces in the C# API.

using System;
using System.Collections.Generic;
using System.Text.Json;
using GoCLR.Runtime;

namespace HclGoClr
{
    /// <summary>Entry point: parse HCL source into JSON or a .NET object graph.</summary>
    public static class Hcl
    {
        /// <summary>Parse native HCL source and return its JSON representation
        /// (the same shape as the hcl2json tool: attributes become keys, blocks
        /// nest by type and labels). Throws HclException on a parse error.</summary>
        public static string ToJson(string filename, string source)
        {
            string res = Interop.HclToJson(filename ?? "config.hcl", source ?? "");
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var err))
                throw new HclException(err.GetString() ?? "parse error");
            return res;
        }

        /// <summary>Parse native HCL source into a .NET object graph
        /// (Dictionary&lt;string,object&gt; for objects/blocks, object[] for lists,
        /// plus bool/long/double/string leaves). Throws HclException on error.</summary>
        public static Dictionary<string, object> Parse(string filename, string source)
        {
            string res = Interop.HclToJson(filename ?? "config.hcl", source ?? "");
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var err))
                throw new HclException(err.GetString() ?? "parse error");
            return (Dictionary<string, object>)FromJson(doc.RootElement);
        }

        private static object FromJson(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => NumberFromJson(e),
            JsonValueKind.Null => null,
            JsonValueKind.Array => ToArray(e),
            JsonValueKind.Object => ToMap(e),
            _ => null,
        };

        // A whole-number token (no '.', 'e' or 'E') that fits in Int64 becomes a long;
        // anything with a fraction or exponent stays a double. cty stores every number
        // as a big.Float, so the JSON alone — not the .NET parse — decides the kind.
        private static object NumberFromJson(JsonElement e)
        {
            string raw = e.GetRawText();
            if (raw.IndexOf('.') < 0 && raw.IndexOf('e') < 0 && raw.IndexOf('E') < 0 &&
                long.TryParse(raw, out var l))
                return l;
            return e.GetDouble();
        }

        private static object[] ToArray(JsonElement e)
        {
            var list = new List<object>();
            foreach (var el in e.EnumerateArray()) list.Add(FromJson(el));
            return list.ToArray();
        }

        private static Dictionary<string, object> ToMap(JsonElement e)
        {
            var m = new Dictionary<string, object>();
            foreach (var p in e.EnumerateObject()) m[p.Name] = FromJson(p.Value);
            return m;
        }
    }

    /// <summary>An HCL parse/evaluation error.</summary>
    public sealed class HclException : Exception
    {
        public HclException(string message) : base(message) { }
    }

    // Thin marshaling layer over the goclr-compiled host (Program.HclToJson),
    // converting System.String ↔ GoString at the boundary.
    internal static class Interop
    {
        // When a goclr-compiled assembly runs as an executable, __goclr_entry runs
        // package initializers + init() before Main. Referenced as a LIBRARY that
        // never runs, we must run the package init ourselves once before the first
        // call, or globals / sync.Once / registries stay unset.
        private static readonly object _gate = new object();
        private static bool _inited;

        private static void EnsureInit()
        {
            if (_inited) return;
            lock (_gate)
            {
                if (_inited) return;
                Program.__goclr_init();
                _inited = true;
            }
        }

        public static string HclToJson(string filename, string source)
        {
            EnsureInit();
            return Program.HclToJson(
                GoString.FromDotNetString(filename),
                GoString.FromDotNetString(source)).ToDotNetString();
        }
    }
}
