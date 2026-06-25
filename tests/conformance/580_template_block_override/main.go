package main

import (
	"os"
	"text/template"
)

func main() {
	// {{block}} defines + invokes a template; a clone's {{define}} overrides it.
	base := template.Must(template.New("base").Parse(
		`START {{block "body" .}}default:{{.}}{{end}} END`))
	clone := template.Must(base.Clone())
	template.Must(clone.Parse(`{{define "body"}}custom:{{.}}{{end}}`))

	must(base.ExecuteTemplate(os.Stdout, "base", "X"))
	os.Stdout.WriteString(" | ")
	must(clone.ExecuteTemplate(os.Stdout, "base", "Y"))
	os.Stdout.WriteString("\n")

	// Deeply nested template invocations resolve through the namespace.
	t := template.Must(template.New("outer").Parse(`O({{template "mid" .}})`))
	template.Must(t.New("mid").Parse(`M[{{template "inner" .}}]`))
	template.Must(t.New("inner").Parse(`I<{{.}}>`))
	must(t.ExecuteTemplate(os.Stdout, "outer", "x"))
	os.Stdout.WriteString("\n")

	// A second clone with a different override is independent of the first.
	clone2 := template.Must(base.Clone())
	template.Must(clone2.Parse(`{{define "body"}}other:{{.}}{{end}}`))
	must(clone2.ExecuteTemplate(os.Stdout, "base", "Z"))
	os.Stdout.WriteString(" ")
	must(clone.ExecuteTemplate(os.Stdout, "base", "W"))
	os.Stdout.WriteString("\n")
}

func must(err error) {
	if err != nil {
		panic(err)
	}
}
