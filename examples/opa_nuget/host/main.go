// Command host is the Go side of the Opa.GoCLR NuGet: it compiles to a .NET
// assembly with goclr and exposes the Open Policy Agent's Rego engine — parser,
// compiler and topdown evaluator — to C# through a tiny JSON bridge. The C# facade
// (see ../Opa.cs) calls OpaEval; everything crossing the boundary is a string, so
// the interop is just GoString ↔ System.String — no ast/rego type leaks into C#.
//
// A caller supplies a Rego module, a query (e.g. "data.example.allow") and the
// input document as JSON. The policy is parsed, prepared and evaluated in-process
// on the CLR; the result set is rendered as JSON in the same shape OPA itself
// produces ([{"expressions":[{"value":...,"text":...}],"bindings":{...}}]).
package main

import (
	"context"
	"encoding/json"
	"fmt"

	"github.com/open-policy-agent/opa/rego"
)

// OpaEval compiles module, prepares query against it, and evaluates it with
// inputJSON as the input document. It returns the OPA result set as a JSON array
// on success, or a JSON object {"error":"..."} on any parse/eval failure.
//
// inputJSON may be empty ("" or "null") for a policy that reads no input.
func OpaEval(module string, query string, inputJSON string) string {
	ctx := context.Background()

	var input interface{}
	if inputJSON != "" {
		if err := json.Unmarshal([]byte(inputJSON), &input); err != nil {
			return errJSON("invalid input JSON: " + err.Error())
		}
	}

	r := rego.New(
		rego.Query(query),
		rego.Module("policy.rego", module),
	)

	prepared, err := r.PrepareForEval(ctx)
	if err != nil {
		return errJSON("prepare: " + err.Error())
	}

	var opts []rego.EvalOption
	if input != nil {
		opts = append(opts, rego.EvalInput(input))
	}
	rs, err := prepared.Eval(ctx, opts...)
	if err != nil {
		return errJSON("eval: " + err.Error())
	}

	b, err := json.Marshal(rs)
	if err != nil {
		return errJSON("cannot render result: " + err.Error())
	}
	return string(b)
}

func errJSON(msg string) string {
	b, _ := json.Marshal(map[string]interface{}{"error": msg})
	return string(b)
}

// main lets the assembly also run standalone as a self-test (goclr build produces
// an assembly with an entry point; C# references it as a library and ignores this).
func main() {
	module := `
package example

default allow = false

allow {
	input.role == "admin"
}

allow {
	input.role == "user"
	input.action == "read"
}
`
	for _, in := range []string{
		`{"role":"admin","action":"delete"}`,
		`{"role":"user","action":"read"}`,
		`{"role":"user","action":"write"}`,
	} {
		fmt.Printf("input=%s => %s\n", in, OpaEval(module, "data.example.allow", in))
	}
}
