package main

import (
	"os"
	"text/template"
)

type Item struct {
	Name  string
	Price float64
}
type Data struct {
	Title string
	Items []Item
	Count int
	Tags  map[string]int
}

func run(s string, data interface{}) {
	template.Must(template.New("x").Parse(s)).Execute(os.Stdout, data)
}

func main() {
	d := Data{
		Title: "Cart",
		Items: []Item{{"Coffee", 3.5}, {"Tea", 2.0}},
		Count: 2,
		Tags:  map[string]int{"hot": 1, "cold": 2},
	}
	run("{{.Title}} ({{.Count}})\n", d)
	run("{{range $i, $it := .Items}}{{$i}}: {{$it.Name}} ${{$it.Price}}\n{{end}}", d)
	run("{{range $k, $v := .Tags}}{{$k}}={{$v}} {{end}}\n", d)
	run("{{if gt .Count 1}}many{{else}}few{{end}}\n", d)
	run("{{with .Items}}{{len .}} items{{end}}\n", d)
	run("{{$n := .Count}}{{if eq $n 2}}two{{end}}\n", d)
	run("{{.Title | printf \"[%s]\"}}\n", d)
	run("len={{len .Items}} first={{index .Items 0 | printf \"%v\"}}\n", d)
	run("{{range .Items}}{{if lt .Price 3.0}}cheap:{{.Name}} {{end}}{{end}}\n", d)
	run("trim:{{- .Count -}}end\n", d)
	run("{{/* comment */}}no-comment\n", d)
	run("nested {{range .Items}}[{{.Name}}={{.Price}}]{{end}}\n", d)

	// sub-templates: define + template, including a forward reference
	t := template.Must(template.New("base").Parse(`{{define "row"}}<{{.}}>{{end}}{{range .}}{{template "row" .}}{{end}}`))
	t.Execute(os.Stdout, []string{"a", "b", "c"})
	os.Stdout.WriteString("\n")

	// and/or/not, ne/ge
	run("{{if and (gt .Count 0) (lt .Count 10)}}in-range{{end}} {{if not .Items}}empty{{else}}full{{end}}\n", d)
}
