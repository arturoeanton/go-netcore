package main

import (
	html "html/template"
	"os"
	"text/template"
)

// (*Template).Delims sets custom action delimiters (default {{ }}). They take effect for
// subsequent Parse calls and work with trim markers, range/if, and html/template's
// contextual escaping. Previously Delims was a no-op (the template was emitted verbatim).
func main() {
	t1 := template.Must(template.New("a").Delims("[[", "]]").Parse(`Hello [[.Name]]! [[range .Items]][[.]] [[end]]`))
	t1.Execute(os.Stdout, struct {
		Name  string
		Items []int
	}{"X", []int{1, 2, 3}})
	os.Stdout.WriteString("\n")

	template.Must(template.New("b").Delims("<%", "%>").Parse("A <%- .X -%> B")).
		Execute(os.Stdout, struct{ X int }{5})
	os.Stdout.WriteString("|\n")

	template.Must(template.New("c").Delims("@", "@").Parse("x@.Y@z")).
		Execute(os.Stdout, struct{ Y string }{"mid"})
	os.Stdout.WriteString("\n")

	// html/template with delims still auto-escapes
	html.Must(html.New("d").Delims("[[", "]]").Parse(`<p>[[.]]</p>`)).
		Execute(os.Stdout, "<script>alert(1)</script>")
	os.Stdout.WriteString("\n")

	// nested control structures with custom delims
	template.Must(template.New("e").Delims("{%", "%}").Parse(`{%range $i, $v := .%}{%if $i%},{%end%}{%$v%}{%end%}`)).
		Execute(os.Stdout, []string{"p", "q", "r"})
	os.Stdout.WriteString("\n")

	// default delims unaffected
	template.Must(template.New("f").Parse("{{.}}")).Execute(os.Stdout, "default")
	os.Stdout.WriteString("\n")
}
