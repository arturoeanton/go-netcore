package main

import (
	"html/template"
	"os"
)

// html/template contextual escaping. A value in a JS string context uses Go's
// jsStrReplacementTable, which escapes the HTML specials, '+', '/', '`' and the control
// chars but NOT '=' (it is not HTML-significant inside a JS string). goclr previously
// over-escaped '=' to =.
func main() {
	data := map[string]any{
		"Name": "<script>alert('xss')</script>",
		"URL":  "javascript:alert(1)",
		"Safe": template.HTML("<b>bold</b>"),
		"Attr": `" onmouseover="alert(1)`,
		"JS":   `a=b; c+d; e/f; '; var x='`,
		"Num":  42,
	}
	tmpl := `<div>{{.Name}}</div>
<a href="{{.URL}}">link</a>
<span>{{.Safe}}</span>
<input value="{{.Attr}}">
<script>var x = "{{.JS}}";</script>
<p data-n="{{.Num}}">{{.Num}}</p>
<a href="/search?q={{.Name}}">q</a>`
	template.Must(template.New("x").Parse(tmpl)).Execute(os.Stdout, data)
	os.Stdout.WriteString("\n")

	template.Must(template.New("css").Parse(`<style>div { color: {{.}} }</style>`)).
		Execute(os.Stdout, "red; background:url('evil')")
	os.Stdout.WriteString("\n")

	os.Stdout.WriteString(template.HTMLEscapeString(`<a href="x">&'`) + "\n")
	os.Stdout.WriteString(template.JSEscapeString(`</script>`) + "\n")
	os.Stdout.WriteString(template.URLQueryEscaper("a b&c") + "\n")
}
