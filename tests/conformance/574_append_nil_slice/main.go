package main

import (
	"bytes"
	"fmt"
	"text/tabwriter"
)

func main() {
	// append(nil) into a [][]T must store a zero slice (not a null), so &s[i] then
	// *p = append(*p, ...) works — the pattern text/tabwriter uses internally.
	var lines [][]int
	lines = append(lines, nil)
	line := &lines[len(lines)-1]
	*line = append(*line, 1, 2, 3)
	lines = append(lines, nil)
	l2 := &lines[len(lines)-1]
	*l2 = append(*l2, 9)
	fmt.Println(lines, len(lines))

	// nil map element keeps map identity.
	var maps []map[string]int
	maps = append(maps, nil)
	fmt.Println(maps[0] == nil, len(maps[0]))

	// Pointer/interface nils stay nil.
	var ps []*int
	ps = append(ps, nil)
	fmt.Println(ps[0] == nil)

	// text/tabwriter end to end (built on the above pattern).
	var buf bytes.Buffer
	w := tabwriter.NewWriter(&buf, 0, 0, 2, ' ', 0)
	fmt.Fprintln(w, "name\tage\tcity")
	fmt.Fprintln(w, "alice\t30\tNYC")
	fmt.Fprintln(w, "bob\t5\tLA")
	w.Flush()
	fmt.Printf("%q\n", buf.String())
}
