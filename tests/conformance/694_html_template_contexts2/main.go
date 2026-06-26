package main

import (
	"html/template"
	"os"
)

// html/template contextual escaping across many positions:
//   - an action in attribute-NAME position is neutralized to ZgotmplZ (Go refuses to escape
//     a dynamic attribute name);
//   - URL values escape every non-safe char ({ } | \ ^ space …) like Go's url normalizer;
//   - a URL whose scheme is not http/https/mailto becomes "#ZgotmplZ" (isSafeURL allowlist).
func main() {
	tests := []struct {
		name, tmpl string
		data       any
	}{
		{"unquoted-attr", `<div class={{.}}>`, "a b"},
		{"single-quoted-attr", `<a title='{{.}}'>`, "it's \"quoted\""},
		{"js-string-bare", `<script>var s = {{.}};</script>`, "hello'world"},
		{"js-obj", `<script>var o = {{.}};</script>`, map[string]int{"a": 1}},
		{"css-bad", `<div style="color: {{.}}">`, "expression(alert(1))"},
		{"href-full", `<a href="{{.}}">`, "https://example.com/path?a=b&c=d"},
		{"href-rel", `<a href="{{.}}">`, "/local/path"},
		{"href-braces", `<a href="{{.}}">`, "a{b}c|d^e"},
		{"href-badscheme", `<a href="{{.}}">`, "telnet:host"},
		{"href-js", `<a href="{{.}}">`, "javascript:alert(1)"},
		{"mailto", `<a href="{{.}}">`, "mailto:x@y.com"},
		{"attr-name-ctx", `<input {{.}}="x">`, "value"},
		{"rcdata", `<textarea>{{.}}</textarea>`, "<b>not bold</b>"},
		{"html-amp", `<p>{{.}}</p>`, "Tom & Jerry < 5"},
	}
	for _, t := range tests {
		os.Stdout.WriteString(t.name + ": ")
		template.Must(template.New(t.name).Parse(t.tmpl)).Execute(os.Stdout, t.data)
		os.Stdout.WriteString("\n")
	}
}
