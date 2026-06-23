package main

import (
	"os"
	"html/template"
)

// html/template's trusted-string types (template.HTML/URL/JS/CSS) bypass the
// contextual auto-escaper — as the data itself, as a map[string]any value, and as a
// struct field — while ordinary strings in the same position are still escaped.
type Page struct {
	Body  template.HTML
	Title string
	Link  template.URL
	Code  template.JS
	Style template.CSS
}

func run(s string, d interface{}) {
	template.Must(template.New("x").Parse(s)).Execute(os.Stdout, d)
	os.Stdout.WriteString("\n")
}

func main() {
	// top-level data
	run(`{{.}}`, template.HTML("<b>raw</b>"))
	run(`{{.}}`, "<b>esc</b>")

	// map[string]any values
	run(`{{.H}} | {{.S}}`, map[string]interface{}{"H": template.HTML("<i>x</i>"), "S": "<i>y</i>"})

	// struct fields, each in its matching context
	p := Page{
		Body:  template.HTML("<b>safe</b>"),
		Title: "<unsafe>",
		Link:  template.URL("javascript:go()"),
		Code:  template.JS("f(1, 2)"),
		Style: template.CSS("color:red"),
	}
	run(`<h1>{{.Title}}</h1><div>{{.Body}}</div>`, p)
	run(`<a href="{{.Link}}">go</a>`, p)
	run(`<script>{{.Code}}</script>`, p)
	run(`<style>.c{ {{.Style}} }</style>`, p)

	// a plain string field in a URL context is still sanitized
	run(`<a href="{{.Title}}">t</a>`, p)
}
