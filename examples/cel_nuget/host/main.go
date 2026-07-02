// Command host is the Go side of the Cel.GoCLR NuGet: it compiles to a .NET
// assembly with goclr and exposes CEL (Google's Common Expression Language) to C#
// through a tiny JSON bridge. The C# facade (see ../Cel.cs) calls CelCompile /
// CelEval; everything flowing across the boundary is a string, so the interop is
// just GoString ↔ System.String — no cty/ref.Val types leak into C#.
//
// A cel.Program is stateless, thread-safe and cacheable, so compiled expressions
// are kept in a process-global registry keyed by an integer handle.
package main

import (
	"encoding/json"
	"fmt"
	"reflect"
	"sync"

	"github.com/google/cel-go/cel"
)

// anyType is the reflect.Type of interface{}, the target for CEL's
// ConvertToNative so a result renders as its natural Go value.
var anyType = reflect.TypeOf((*any)(nil)).Elem()

var (
	mu       sync.Mutex
	programs = map[int64]cel.Program{}
	nextID   int64
)

// CelCompile compiles a CEL expression whose free variables are the names in
// varNamesJSON (a JSON array like ["user","age"]), each declared as the dynamic
// type so any JSON value binds. It returns a JSON object: {"handle":N} on success
// or {"error":"..."} if the expression does not type-check.
func CelCompile(expr string, varNamesJSON string) string {
	var names []string
	if varNamesJSON != "" {
		if err := json.Unmarshal([]byte(varNamesJSON), &names); err != nil {
			return errJSON("invalid variable list: " + err.Error())
		}
	}
	opts := make([]cel.EnvOption, 0, len(names))
	for _, n := range names {
		opts = append(opts, cel.Variable(n, cel.DynType))
	}
	env, err := cel.NewEnv(opts...)
	if err != nil {
		return errJSON(err.Error())
	}
	ast, iss := env.Compile(expr)
	if iss.Err() != nil {
		return errJSON(iss.Err().Error())
	}
	prg, err := env.Program(ast)
	if err != nil {
		return errJSON(err.Error())
	}
	mu.Lock()
	nextID++
	id := nextID
	programs[id] = prg
	mu.Unlock()
	return fmt.Sprintf(`{"handle":%d}`, id)
}

// CelEval evaluates a compiled program (by handle) against the variables in
// varsJSON (a JSON object). It returns a JSON object: {"value":<result>} on
// success or {"error":"..."} on a binding/evaluation error. The result is
// rendered from CEL's native value, so a bool/int/string/list/map round-trips as
// the matching JSON.
func CelEval(handle int64, varsJSON string) string {
	mu.Lock()
	prg, ok := programs[handle]
	mu.Unlock()
	if !ok {
		return errJSON("unknown program handle")
	}
	vars := map[string]any{}
	if varsJSON != "" {
		if err := json.Unmarshal([]byte(varsJSON), &vars); err != nil {
			return errJSON("invalid variables JSON: " + err.Error())
		}
	}
	out, _, err := prg.Eval(vars)
	if err != nil {
		return errJSON(err.Error())
	}
	native, err := out.ConvertToNative(anyType)
	if err != nil {
		// Fall back to the ref.Val's Go value directly.
		native = out.Value()
	}
	b, err := json.Marshal(map[string]any{"value": native})
	if err != nil {
		return errJSON("cannot render result: " + err.Error())
	}
	return string(b)
}

func errJSON(msg string) string {
	b, _ := json.Marshal(map[string]any{"error": msg})
	return string(b)
}

// main lets the assembly also run standalone as a self-test (goclr build produces
// an assembly with an entry point; C# references it as a library and ignores this).
func main() {
	h := CelCompile(`user.role == "admin" || user.id == doc.owner`, `["user","doc"]`)
	fmt.Println("compile:", h)
	fmt.Println("eval-1:", CelEval(1, `{"user":{"role":"admin","id":1},"doc":{"owner":2}}`))
	fmt.Println("eval-2:", CelEval(1, `{"user":{"role":"guest","id":2},"doc":{"owner":2}}`))
	fmt.Println("eval-3:", CelEval(1, `{"user":{"role":"guest","id":9},"doc":{"owner":2}}`))
	h2 := CelCompile(`[1, 2, 3].map(x, x * n)`, `["n"]`)
	fmt.Println("compile2:", h2)
	fmt.Println("eval-map:", CelEval(2, `{"n":10}`))
}
