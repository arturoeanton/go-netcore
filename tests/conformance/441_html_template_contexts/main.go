package main

import (
	"os"
	"html/template"
)

// html/template's contextual auto-escaper distinguishes URL path vs query parts,
// bare JS values vs JS-string bodies, event-handler (onclick) JS attributes, and
// filters dangerous CSS values to ZgotmplZ.
func run(s string, d interface{}) {
	template.Must(template.New("x").Parse(s)).Execute(os.Stdout, d)
	os.Stdout.WriteString("\n")
}

func main() {
	// URL: value after '?' is query-encoded ('&' -> %26); before '?' it is normalized.
	run(`<a href="/q?s={{.}}">x</a>`, "a&b c")
	run(`<a href="{{.}}">x</a>`, "a&b c")
	run(`<a href="{{.}}">x</a>`, "/p?x=1&y=2 z")

	// JS: a bare value is JSON-encoded ('/' kept); inside a string '/' becomes \/.
	run(`<script>var x={{.}};</script>`, "</script><x>")
	run(`<script>var s="{{.}}";</script>`, "a\"/b</script>")
	run(`<script>var n={{.}};</script>`, 42)

	// event-handler attribute: the value is treated as a JS value, then attr-escaped.
	run(`<button onclick="{{.}}">go</button>`, `f('x')`)
	run(`<button onclick={{.}}>go</button>`, `f('x')`)

	// CSS value filtering: dangerous values collapse to ZgotmplZ; safe ones pass.
	run(`<style>p{color:{{.}}}</style>`, "red;}")
	run(`<style>p{color:{{.}}}</style>`, "rgb(1,2,3)")
	run(`<style>p{margin:{{.}}}</style>`, "1px solid red")
	run(`<style>p{color:{{.}}}</style>`, "#abc")

	// the style="" attribute is also a CSS context (filtered, then attribute-escaped).
	run(`<p style="color:{{.}}">x</p>`, "red")
	run(`<p style="color:{{.}}">x</p>`, "red;}")
	run(`<p style='width:{{.}}'>x</p>`, "100%")
}
