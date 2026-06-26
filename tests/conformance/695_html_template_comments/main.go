package main

import (
	"html/template"
	"os"
	tt "text/template"
)

// html/template elides HTML comments (<!-- … -->) from the output, including any actions
// inside them, exactly as Go does — even comments spanning a text/action boundary. A
// <!DOCTYPE …> declaration is preserved (it is not a comment), and a later contextual
// action (e.g. a URL attribute) still gets the right escaper. text/template keeps comments
// verbatim. (goclr previously emitted html comments literally.)
func main() {
	exec := func(s string, d any) {
		template.Must(template.New("h").Parse(s)).Execute(os.Stdout, d)
		os.Stdout.WriteString("\n")
	}
	exec(`<div><!-- hidden {{.}} -->visible</div>`, "X")
	exec(`a<!-- c1 -->b<!-- c2 {{.}} -->c`, "Y")
	exec(`<!-- whole comment {{.}} -->`, "Z")
	exec(`<!DOCTYPE html><html><a href="{{.}}">x</a></html>`, "https://e.com/p?q=1&r=2")
	exec(`before<!--c-->after`, nil)
	exec(`<p>{{.}}</p>`, "kept & shown")

	// text/template keeps comments verbatim
	tt.Must(tt.New("t").Parse(`<!-- kept {{.}} -->`)).Execute(os.Stdout, "T")
	os.Stdout.WriteString("\n")
}
