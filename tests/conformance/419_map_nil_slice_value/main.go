// map/slice with a slice-typed value: storing nil and reading it back must yield the
// zero slice, not a null (which would NRE on unbox). Mirrors fiber's buildTree treeStack.
package main

import "fmt"

type Route struct{ name string }

func main() {
	// map[string][]int: set nil, read back, append.
	mi := map[string][]int{}
	mi["k"] = nil
	fmt.Println("int nil:", mi["k"] == nil, len(mi["k"]))
	mi["k"] = append(mi["k"], 1, 2)
	fmt.Println("int appended:", mi["k"])

	// map[string][]*Route stored in a slice element (treeStack shape).
	treeStack := make([]map[string][]*Route, 2)
	tsMap := make(map[string][]*Route, 1)
	tsMap[""] = nil
	tsMap[""] = append(tsMap[""], &Route{"a"}, &Route{"b"})
	treeStack[0] = tsMap
	fmt.Println("nested len:", len(treeStack[0][""]), treeStack[0][""][1].name)

	// slice element of slice type set to nil, then read.
	rows := make([][]string, 3)
	rows[1] = nil
	fmt.Println("row nil:", rows[1] == nil, len(rows[1]))
	rows[1] = append(rows[1], "x")
	fmt.Println("row:", rows[1])
}
