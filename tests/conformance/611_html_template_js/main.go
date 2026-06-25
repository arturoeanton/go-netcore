package main

import (
	"html/template"
	"os"
)

// html/template's JS context marshals a non-scalar value (map/slice/struct) as JSON,
// while scalars are emitted as JS literals; HTML/attr/URL contexts auto-escape as before.
type Cfg struct {
	Name    string `json:"name"`
	Count   int    `json:"count"`
	Enabled bool   `json:"enabled"`
}

func main() {
	js := template.Must(template.New("t").Parse(`<script>var x = {{.}};</script>`))
	for _, v := range []interface{}{
		[]int{1, 2, 3},
		[]string{"a<b", "c&d"},
		map[string]interface{}{"k": "v", "n": 5},
		Cfg{Name: "test<>", Count: 3, Enabled: true},
		"plain",
		42, 3.14, true, nil,
		[]map[string]int{{"a": 1}, {"b": 2}},
	} {
		js.Execute(os.Stdout, v)
		os.Stdout.WriteString("\n")
	}

	// inside a JS string literal
	jstr := template.Must(template.New("t").Parse(`<script>var s = "{{.}}";</script>`))
	jstr.Execute(os.Stdout, `he said "hi" & </script>`)
	os.Stdout.WriteString("\n")

	// other contexts still escape
	h := template.Must(template.New("t").Parse(`<p>{{.}}</p>`))
	h.Execute(os.Stdout, "<script>x</script>")
	os.Stdout.WriteString("\n")
	a := template.Must(template.New("t").Parse(`<a href="/x?q={{.}}">l</a>`))
	a.Execute(os.Stdout, "a b&c")
	os.Stdout.WriteString("\n")
}
