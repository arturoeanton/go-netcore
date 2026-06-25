package main

import (
	"fmt"
	"maps"
	"slices"
)

// maps package, non-iterator functions: Clone (shallow copy), Copy (merge into dst), Equal,
// EqualFunc, DeleteFunc. Output stays order-independent by sorting keys before printing.
func sortedKeys(m map[string]int) []string {
	ks := []string{}
	for k := range m {
		ks = append(ks, k)
	}
	slices.Sort(ks)
	return ks
}

func main() {
	m := map[string]int{"a": 1, "b": 2, "c": 3}
	m2 := maps.Clone(m)
	m2["d"] = 4
	fmt.Println(len(m), len(m2), m2["a"], sortedKeys(m2))

	dst := map[string]int{"x": 9}
	maps.Copy(dst, m)
	fmt.Println(len(dst), dst["a"], dst["x"], sortedKeys(dst))

	fmt.Println(maps.Equal(m, map[string]int{"a": 1, "b": 2, "c": 3}))
	fmt.Println(maps.Equal(m, map[string]int{"a": 1}))
	fmt.Println(maps.Equal(map[string]int{}, map[string]int{}))
	fmt.Println(maps.EqualFunc(map[string]int{"a": 1}, map[string]int{"a": 2}, func(v1, v2 int) bool { return v1 <= v2 }))

	maps.DeleteFunc(m, func(k string, v int) bool { return v > 1 })
	fmt.Println(sortedKeys(m), m["a"])

	// Clone of nil map is nil.
	var nilm map[string]int
	fmt.Println(maps.Clone(nilm) == nil)
}
