package main

import (
	"os"
	"text/template"
)

func main() {
	t := template.Must(template.New("t").Parse(
		`str[1:3]={{slice .Str 1 3}} str[2:]={{slice .Str 2}} str[:]={{slice .Str}} ` +
			`sl[1:3]={{slice .S 1 3}} sl[1:]={{slice .S 1}} first={{index (slice .S 2) 0}} ` +
			`empty={{slice .Str 2 2}}`))
	if err := t.Execute(os.Stdout, map[string]any{
		"Str": "hello world",
		"S":   []int{10, 20, 30, 40, 50},
	}); err != nil {
		panic(err)
	}
	os.Stdout.WriteString("\n")
}
