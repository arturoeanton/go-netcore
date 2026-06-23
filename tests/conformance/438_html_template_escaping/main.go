package main

import (
	"os"
	"html/template"
)

// html/template applies contextual auto-escaping: HTML text, attribute values
// (quoted and unquoted), URL attributes (with javascript: sanitized to #ZgotmplZ),
// <script> JS context, and <style> CSS context.
func run(s string, d interface{}) {
	template.Must(template.New("x").Parse(s)).Execute(os.Stdout, d)
	os.Stdout.WriteString("\n")
}

func main() {
	run(`<p>{{.}}</p>`, "<b>hi</b> & 'go'")
	run(`<ul>{{range .}}<li>{{.}}</li>{{end}}</ul>`, []string{"a<b>", "c&d"})
	run(`<a class="{{.Cls}}" href="{{.URL}}">{{.Txt}}</a>`, map[string]string{"Cls": "x y", "URL": "/p?a=1&b=2", "Txt": "<hi>"})
	run(`<p title={{.}}>x</p>`, "un quoted=v")
	run(`<input value='{{.}}'>`, `it's "quoted"`)
	run(`<a href="{{.}}">x</a>`, "javascript:alert(1)")
	run(`<a href="{{.}}">y</a>`, "https://example.com/?q=a b")
	run(`<script>var s = "{{.S}}"; var n = {{.N}};</script>`, map[string]interface{}{"S": "</script><x>/", "N": 42})
	run(`<style>.c{color:{{.}}}</style>`, "red")
	run(`{{if .}}has{{else}}none{{end}} {{.}}`, "<v>")
}
