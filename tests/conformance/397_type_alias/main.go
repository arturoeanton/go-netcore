// Type aliases (Go 1.22+ models them as *types.Alias). goclr unaliases in goType so
// `*os.PathError` (= *fs.PathError) compiles in a type switch and user aliases lower.
package main

import (
	"fmt"
	"os"
)

type Point struct{ X, Y int }
type P = Point        // alias to a struct
type Meters = float64 // alias to a basic type

func classify(err error) string {
	switch err.(type) {
	case *os.PathError: // an aliased struct pointer in a type switch
		return "path"
	default:
		return "other"
	}
}

func main() {
	p := &P{X: 1, Y: 2}
	var d Meters = 2.5
	fmt.Println(p.X, p.Y, d)
	fmt.Println(classify(fmt.Errorf("plain")))
}
