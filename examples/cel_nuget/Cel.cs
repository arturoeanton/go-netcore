// Cel.GoCLR — a C# facade over Google's CEL (Common Expression Language),
// implemented by compiling the production Go cel-go library to a .NET assembly
// with goclr. No sidecar, no P/Invoke: the compiled CEL runs in-process on the CLR.
//
//   var program = Cel.Compile(
//       "user.role in ['admin','mod'] || user.id == doc.owner",
//       "user", "doc");
//   bool ok = program.EvaluateBool(new {
//       user = new { role = "guest", id = 2 },
//       doc  = new { owner = 2 },
//   });
//
// The bridge is string-only (GoString ↔ System.String); values cross as JSON, so
// no cel-go type ever surfaces in the C# API.

using System;
using System.Text.Json;
using GoCLR.Runtime;

namespace CelGoClr
{
    /// <summary>A compiled, reusable CEL expression. Thread-safe and cacheable
    /// (a cel.Program is stateless), so compile once and evaluate many times.</summary>
    public sealed class CelProgram
    {
        private readonly long _handle;
        internal CelProgram(long handle) => _handle = handle;

        /// <summary>Evaluate against a variable object (anonymous types, POCOs and
        /// dictionaries all work — they are serialized to JSON). Returns the CEL
        /// result deserialized to the natural .NET type (bool, long, string,
        /// object[], Dictionary&lt;string,object&gt;).</summary>
        public object Evaluate(object variables)
        {
            string varsJson = variables == null ? "{}" : JsonSerializer.Serialize(variables);
            string res = Interop.CelEval(_handle, varsJson);
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.TryGetProperty("error", out var err))
                throw new CelException(err.GetString() ?? "evaluation error");
            return FromJson(doc.RootElement.GetProperty("value"));
        }

        /// <summary>Evaluate expecting a boolean result (the common authorization /
        /// policy case). Throws if the result is not a bool.</summary>
        public bool EvaluateBool(object variables)
        {
            var v = Evaluate(variables);
            if (v is bool b) return b;
            throw new CelException($"expected a boolean result, got {v?.GetType().Name ?? "null"}");
        }

        /// <summary>Evaluate and return the raw result JSON (for callers that want to
        /// deserialize it themselves).</summary>
        public string EvaluateJson(object variables)
        {
            string varsJson = variables == null ? "{}" : JsonSerializer.Serialize(variables);
            return Interop.CelEval(_handle, varsJson);
        }

        private static object FromJson(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
            JsonValueKind.Null => null,
            JsonValueKind.Array => ToArray(e),
            JsonValueKind.Object => ToMap(e),
            _ => null,
        };

        private static object[] ToArray(JsonElement e)
        {
            var list = new System.Collections.Generic.List<object>();
            foreach (var el in e.EnumerateArray()) list.Add(FromJson(el));
            return list.ToArray();
        }

        private static System.Collections.Generic.Dictionary<string, object> ToMap(JsonElement e)
        {
            var m = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var p in e.EnumerateObject()) m[p.Name] = FromJson(p.Value);
            return m;
        }
    }

    /// <summary>Entry point: compile a CEL expression into a reusable program.</summary>
    public static class Cel
    {
        /// <summary>Compile a CEL expression whose free variables are the given
        /// names (each bound dynamically at evaluation). Throws CelException if the
        /// expression does not parse or type-check.</summary>
        public static CelProgram Compile(string expression, params string[] variables)
        {
            string namesJson = JsonSerializer.Serialize(variables ?? Array.Empty<string>());
            string res = Interop.CelCompile(expression, namesJson);
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.TryGetProperty("error", out var err))
                throw new CelException(err.GetString() ?? "compile error");
            return new CelProgram(doc.RootElement.GetProperty("handle").GetInt64());
        }
    }

    /// <summary>A CEL parse/type-check/evaluation error.</summary>
    public sealed class CelException : Exception
    {
        public CelException(string message) : base(message) { }
    }

    // Thin marshaling layer over the goclr-compiled host (Program.CelCompile /
    // Program.CelEval), converting System.String ↔ GoString at the boundary.
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

        public static string CelCompile(string expr, string namesJson)
        {
            EnsureInit();
            return Program.CelCompile(GoString.FromDotNetString(expr), GoString.FromDotNetString(namesJson)).ToDotNetString();
        }

        public static string CelEval(long handle, string varsJson)
        {
            EnsureInit();
            return Program.CelEval(handle, GoString.FromDotNetString(varsJson)).ToDotNetString();
        }
    }
}
