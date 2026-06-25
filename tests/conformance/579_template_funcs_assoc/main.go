package main

import (
	"os"
	"strings"
	"text/template"
)

func main() {
	// Custom funcs receive the underlying values (a []string from a map[string]any).
	t := template.Must(template.New("main").Funcs(template.FuncMap{
		"join":  strings.Join,
		"upper": strings.ToUpper,
		"rep":   strings.Repeat,
	}).Parse(`{{join .Tags ", "}} | {{upper .Name}} | {{rep "ab" 3}}`))
	must(t.Execute(os.Stdout, map[string]any{
		"Tags": []string{"go", "clr", "net"},
		"Name": "hello",
	}))
	os.Stdout.WriteString("\n")

	// Associated templates created via New share one namespace.
	base := template.Must(template.New("base").Parse(`<{{template "body" .}}>`))
	template.Must(base.New("body").Parse(`{{.Title}}:{{template "inner" .}}`))
	template.Must(base.New("inner").Parse(`[{{.N}}]`))
	must(base.ExecuteTemplate(os.Stdout, "base", map[string]any{"Title": "T", "N": 42}))
	os.Stdout.WriteString("\n")

	// define + template + range + custom func together.
	t2 := template.Must(template.New("t").Funcs(template.FuncMap{"join": strings.Join}).Parse(
		`{{- define "row"}}{{.Name}}={{.Val}}{{end -}}
{{range $i, $e := .Items}}{{if $i}}, {{end}}{{template "row" $e}}{{end}} :: {{join .Tags "/"}}`))
	type Item struct {
		Name string
		Val  int
	}
	must(t2.Execute(os.Stdout, map[string]any{
		"Items": []Item{{"x", 1}, {"y", 2}, {"z", 3}},
		"Tags":  []string{"p", "q"},
	}))
	os.Stdout.WriteString("\n")
}

func must(err error) {
	if err != nil {
		panic(err)
	}
}
