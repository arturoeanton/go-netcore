// Opa.GoCLR — a C# facade over the Open Policy Agent's Rego engine, implemented by
// compiling the production Go open-policy-agent/opa library to a .NET assembly with
// goclr. No sidecar, no P/Invoke: the Rego parser, compiler and topdown evaluator
// run in-process on the CLR.
//
//   // Evaluate a policy and read the first expression value:
//   var opa = new OpaPolicy(module);
//   bool allow = opa.EvalBool("data.example.allow",
//                             new { role = "admin", action = "delete" });
//
//   // Or get the raw OPA result set as JSON:
//   string json = opa.Eval("data.example.allow", "{\"role\":\"admin\"}");
//
// The bridge is string-only (GoString ↔ System.String); policy, query and input all
// cross to Go as strings, and results come back as JSON — so no ast/rego type ever
// surfaces in the C# API.

using System;
using System.Text.Json;
using GoCLR.Runtime;

namespace OpaGoClr
{
    /// <summary>A Rego policy prepared for evaluation on the CLR. Reusable across
    /// many inputs. The policy text is compiled by OPA on each Eval call (OPA's own
    /// PrepareForEval), so hold onto one instance and vary the input.</summary>
    public sealed class OpaPolicy
    {
        private readonly string _module;

        /// <summary>Create a policy from a Rego module (the full `package ...` text).</summary>
        public OpaPolicy(string module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
        }

        /// <summary>Evaluate query (e.g. "data.example.allow") against this policy
        /// with the given input document (raw JSON, or null/"" for no input) and
        /// return OPA's result set as JSON:
        /// [{"expressions":[{"value":...,"text":...,"location":...}],"bindings":{...}}].
        /// Throws OpaException on a parse/compile/eval error.</summary>
        public string Eval(string query, string inputJson = null)
        {
            string res = Interop.OpaEval(_module, query ?? "", inputJson ?? "");
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var err))
                throw new OpaException(err.GetString() ?? "eval error");
            return res;
        }

        /// <summary>Evaluate query with input serialized to JSON (any object, or
        /// null for no input). Same result JSON as <see cref="Eval(string,string)"/>.</summary>
        public string Eval(string query, object input)
            => Eval(query, input == null ? null : JsonSerializer.Serialize(input));

        /// <summary>Evaluate query and return the first expression's value as a bool.
        /// Convenience for boolean authorization rules (data.pkg.allow). Returns false
        /// if the result set is empty (query undefined). Throws OpaException on error
        /// or OpaException if the value is not a boolean.</summary>
        public bool EvalBool(string query, object input = null)
        {
            using var doc = JsonDocument.Parse(Eval(query, input));
            JsonElement root = doc.RootElement;
            if (root.GetArrayLength() == 0) return false;
            JsonElement value = FirstValue(root);
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new OpaException($"query {query} did not evaluate to a boolean"),
            };
        }

        /// <summary>Evaluate query and return the first expression's value as a
        /// JsonElement (owned by a JsonDocument the caller must dispose). Returns a
        /// disposable holder so `using` cleans up. Throws if the result set is empty.</summary>
        public ResultValue EvalValue(string query, object input = null)
        {
            var doc = JsonDocument.Parse(Eval(query, input));
            try
            {
                if (doc.RootElement.GetArrayLength() == 0)
                    throw new OpaException($"query {query} is undefined (empty result set)");
                return new ResultValue(doc, FirstValue(doc.RootElement));
            }
            catch
            {
                doc.Dispose();
                throw;
            }
        }

        // The value of the first expression of the first result:
        //   [ { "expressions": [ { "value": <here> } ] } ]
        private static JsonElement FirstValue(JsonElement resultSet)
            => resultSet[0].GetProperty("expressions")[0].GetProperty("value");
    }

    /// <summary>Owns the JsonDocument backing a query's value; dispose when done.</summary>
    public sealed class ResultValue : IDisposable
    {
        private readonly JsonDocument _doc;
        /// <summary>The first-expression value from the OPA result set.</summary>
        public JsonElement Value { get; }
        internal ResultValue(JsonDocument doc, JsonElement value) { _doc = doc; Value = value; }
        public void Dispose() => _doc.Dispose();
    }

    /// <summary>A Rego parse/compile/evaluation error.</summary>
    public sealed class OpaException : Exception
    {
        public OpaException(string message) : base(message) { }
    }

    // Thin marshaling layer over the goclr-compiled host (Program.OpaEval),
    // converting System.String ↔ GoString at the boundary.
    internal static class Interop
    {
        // When a goclr-compiled assembly runs as an executable, __goclr_entry runs
        // package initializers + init() before Main. Referenced as a LIBRARY that
        // never runs, we must run the package init ourselves once before the first
        // call, or globals / sync.Once / registries (OPA's builtin tables) stay unset.
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

        public static string OpaEval(string module, string query, string inputJson)
        {
            EnsureInit();
            return Program.OpaEval(
                GoString.FromDotNetString(module),
                GoString.FromDotNetString(query),
                GoString.FromDotNetString(inputJson)).ToDotNetString();
        }
    }
}
