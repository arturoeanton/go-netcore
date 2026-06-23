package main

import (
	"fmt"
	"os"
	"text/template"
)

// exec parses and executes, printing either the rendered output or the error
// message — exercising the error paths deterministically (no panic/Must).
func exec(s string, data interface{}) {
	t, err := template.New("x").Parse(s)
	if err != nil {
		fmt.Printf("parse-err\n")
		return
	}
	if err := t.Execute(os.Stdout, data); err != nil {
		fmt.Printf("|exec-err\n")
		return
	}
	fmt.Printf("|ok\n")
}

func main() {
	// index by map string key, by slice position, by string position
	exec(`{{index . "k"}}`, map[string]int{"k": 99})
	exec(`{{index .S 1}}`, map[string]interface{}{"S": []int{7, 8, 9}})
	exec(`{{index .Str 0}}`, map[string]interface{}{"Str": "AB"})

	// eq across incompatible basic kinds is an error in Go
	exec(`{{eq 1 1.0}}`, nil)
	// eq within the same kind is fine
	exec(`{{eq 2 2}}`, nil)

	// unmatched {{end}} is a parse error
	exec(`hi{{end}}`, nil)
	// well-formed if/else
	exec(`{{if true}}T{{else}}F{{end}}`, nil)
}
