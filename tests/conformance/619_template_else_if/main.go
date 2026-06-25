package main

import (
	"fmt"
	htmpl "html/template"
	"os"
	"text/template"
)

// text/template (and html/template) {{else if PIPE}} is sugar for a nested {{if}}
// sharing the SAME single {{end}}. The chained form — including several in a row,
// with or without a trailing {{else}}, and nesting inside an else-if branch — must
// parse and execute exactly like Go.
func main() {
	t := template.Must(template.New("x").Parse(
		`{{if eq . 1}}one{{else if eq . 2}}two{{else if eq . 3}}three{{else if eq . 4}}four{{else}}many{{end}}`))
	for _, n := range []int{1, 2, 3, 4, 5} {
		t.Execute(os.Stdout, n)
		fmt.Print(" ")
	}
	fmt.Println()

	// chained else-if with NO trailing else
	t2 := template.Must(template.New("y").Parse(`{{if gt . 10}}big{{else if gt . 5}}med{{end}}`))
	for _, n := range []int{3, 7, 20} {
		t2.Execute(os.Stdout, n)
		fmt.Print("|")
	}
	fmt.Println()

	// nested if inside an else-if branch
	t3 := template.Must(template.New("z").Parse(
		`{{if .A}}A{{else if .B}}{{if .C}}BC{{else}}B{{end}}{{else}}none{{end}}`))
	t3.Execute(os.Stdout, map[string]bool{"A": false, "B": true, "C": true})
	fmt.Print(" ")
	t3.Execute(os.Stdout, map[string]bool{"A": false, "B": true, "C": false})
	fmt.Print(" ")
	t3.Execute(os.Stdout, map[string]bool{"A": false, "B": false, "C": false})
	fmt.Println()

	// html/template chained else-if with contextual auto-escaping
	h := htmpl.Must(htmpl.New("h").Parse(
		`{{if eq .K "x"}}<b>{{.V}}</b>{{else if eq .K "y"}}{{.V}}{{else}}none{{end}}`))
	h.Execute(os.Stdout, map[string]string{"K": "y", "V": "<script>"})
	fmt.Println()
}
