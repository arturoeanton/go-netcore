package main

import (
	"fmt"
	htmpl "html/template"
	"os"
	"text/template"
)

// (*Template).Lookup returns the named template or nil when it doesn't exist (it must
// NOT return the receiver). ExecuteTemplate of a missing name errors. Same for
// html/template, which shares the implementation.
func main() {
	t := template.Must(template.New("x").Parse(`{{define "a"}}A{{end}}{{define "b"}}B{{end}}main`))
	fmt.Println(t.Lookup("a") != nil, t.Lookup("b") != nil, t.Lookup("z") == nil, t.Lookup("x") != nil)

	// ExecuteTemplate by name, and the name of a looked-up template
	t.ExecuteTemplate(os.Stdout, "a", nil)
	t.ExecuteTemplate(os.Stdout, "b", nil)
	fmt.Println()
	fmt.Println(t.Lookup("a").Name())

	// ExecuteTemplate of a missing template returns an error
	err := t.ExecuteTemplate(os.Stdout, "missing", nil)
	fmt.Println(err != nil)

	// the receiver template is itself lookup-able by its own name
	fmt.Println(t.Lookup("x").Name())

	// html/template behaves the same
	h := htmpl.Must(htmpl.New("h").Parse(`{{define "safe"}}<b>{{.}}</b>{{end}}content`))
	fmt.Println(h.Lookup("safe") != nil, h.Lookup("nope") == nil)
	h.ExecuteTemplate(os.Stdout, "safe", "x<y")
	fmt.Println()
}
