// Command host is the Go side of the Hcl.GoCLR NuGet: it compiles to a .NET
// assembly with goclr and exposes HashiCorp's HCL (HashiCorp Configuration
// Language) to C# through a tiny JSON bridge. The C# facade (see ../Hcl.cs) calls
// HclToJson; everything crossing the boundary is a string, so the interop is just
// GoString ↔ System.String — no cty/hcl types leak into C#.
//
// The conversion walks the parsed HCL body generically (attributes + nested
// blocks, with block labels), evaluating each attribute to a cty value and
// rendering the whole document as JSON. This is the same shape produced by the
// well-known hcl2json tool, so any flat-or-nested HCL config round-trips to a
// JSON object without the caller having to describe a schema up front.
package main

import (
	"encoding/json"
	"fmt"

	"github.com/hashicorp/hcl/v2"
	"github.com/hashicorp/hcl/v2/hclsyntax"
	"github.com/zclconf/go-cty/cty"
	ctyjson "github.com/zclconf/go-cty/cty/json"
)

// HclToJson parses native HCL source and returns its JSON representation.
// On success it returns the config as a JSON object ({"json": <obj>} is NOT used;
// the object is returned directly). On a parse/evaluation error it returns a JSON
// object {"error":"..."}. filename is used only for diagnostics.
func HclToJson(filename string, src string) string {
	file, diags := hclsyntax.ParseConfig([]byte(src), filename, hcl.Pos{Line: 1, Column: 1})
	if diags.HasErrors() {
		return errJSON(diags.Error())
	}
	body, ok := file.Body.(*hclsyntax.Body)
	if !ok {
		return errJSON("unexpected body type")
	}
	obj, err := bodyToMap(body)
	if err != nil {
		return errJSON(err.Error())
	}
	b, err := json.Marshal(obj)
	if err != nil {
		return errJSON("cannot render JSON: " + err.Error())
	}
	return string(b)
}

// bodyToMap converts an hclsyntax.Body to a map[string]interface{}: each attribute
// becomes a key→native-value entry, and each block becomes a nested structure keyed
// by the block type, with labels forming intermediate map levels. Repeated blocks of
// the same type accumulate into a list.
func bodyToMap(body *hclsyntax.Body) (map[string]interface{}, error) {
	out := map[string]interface{}{}

	for name, attr := range body.Attributes {
		val, diags := attr.Expr.Value(nil)
		if diags.HasErrors() {
			return nil, fmt.Errorf("attribute %q: %s", name, diags.Error())
		}
		native, err := ctyToNative(val)
		if err != nil {
			return nil, err
		}
		out[name] = native
	}

	for _, block := range body.Blocks {
		inner, err := bodyToMap(block.Body)
		if err != nil {
			return nil, err
		}
		// Nest label levels: block "a" "b" { ... } => {type:{a:{b:{...}}}}.
		leaf := inner
		for i := len(block.Labels) - 1; i >= 0; i-- {
			leaf = map[string]interface{}{block.Labels[i]: leaf}
		}
		appendBlock(out, block.Type, leaf)
	}

	return out, nil
}

// appendBlock stores value under key, turning the slot into a list if a value for
// that key already exists (repeated blocks of the same type).
func appendBlock(out map[string]interface{}, key string, value interface{}) {
	existing, ok := out[key]
	if !ok {
		out[key] = value
		return
	}
	if list, ok := existing.([]interface{}); ok {
		out[key] = append(list, value)
		return
	}
	out[key] = []interface{}{existing, value}
}

// ctyToNative converts a cty.Value to a plain Go value (bool, float64, string,
// []interface{}, map[string]interface{}) suitable for json.Marshal. It routes
// through ctyjson so number precision and collection shapes match cty's own rules.
func ctyToNative(v cty.Value) (interface{}, error) {
	if v.IsNull() {
		return nil, nil
	}
	b, err := ctyjson.Marshal(v, v.Type())
	if err != nil {
		return nil, err
	}
	var native interface{}
	if err := json.Unmarshal(b, &native); err != nil {
		return nil, err
	}
	return native, nil
}

func errJSON(msg string) string {
	b, _ := json.Marshal(map[string]interface{}{"error": msg})
	return string(b)
}

// main lets the assembly also run standalone as a self-test (goclr build produces
// an assembly with an entry point; C# references it as a library and ignores this).
func main() {
	src := `
io_mode = "async"
retries = 3
service "http" "web_proxy" {
  listen_addr = "127.0.0.1:8080"
  enabled     = true
}
service "https" "web_proxy" {
  listen_addr = "127.0.0.1:8443"
}
`
	fmt.Println("tojson:", HclToJson("config.hcl", src))
}
