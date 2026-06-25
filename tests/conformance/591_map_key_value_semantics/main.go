package main

import "fmt"

// Map keys use Go value equality: arrays and structs holding array fields hash and
// compare by contents, so equal-by-value keys collide; pointers keep identity.
type Key struct {
	A [2]int
	S string
}
type Vec struct{ X, Y float64 }
type Color int

func main() {
	// struct-with-array key: equal-by-value instances are the same key.
	m := map[Key]int{}
	m[Key{[2]int{1, 2}, "x"}] = 10
	m[Key{[2]int{1, 3}, "x"}] = 20
	fmt.Println(m[Key{[2]int{1, 2}, "x"}], m[Key{[2]int{1, 3}, "x"}], len(m))

	// array key: two equal literals collapse to one entry.
	am := map[[2]int]string{}
	am[[2]int{1, 1}] = "a"
	am[[2]int{1, 1}] = "b"
	am[[2]int{2, 2}] = "c"
	fmt.Println(len(am), am[[2]int{1, 1}])

	// array of strings key, with compound assign.
	sm := map[[2]string]int{}
	sm[[2]string{"a", "b"}] = 1
	sm[[2]string{"a", "b"}]++
	fmt.Println(len(sm), sm[[2]string{"a", "b"}])

	// named scalar and float-struct keys.
	cm := map[Color]string{Color(1): "a", Color(2): "b"}
	fmt.Println(cm[Color(1)], cm[Color(2)], len(cm))
	vm := map[Vec]int{}
	vm[Vec{1.5, 2.5}] = 1
	vm[Vec{1.5, 2.5}] = 2
	fmt.Println(len(vm), vm[Vec{1.5, 2.5}])

	// nested array-in-struct-in-array key.
	type Cell struct{ V [2]int }
	type Grid struct{ Cells [2]Cell }
	gm := map[Grid]string{}
	g := Grid{[2]Cell{{[2]int{1, 2}}, {[2]int{3, 4}}}}
	gm[g] = "x"
	fmt.Println(gm[Grid{[2]Cell{{[2]int{1, 2}}, {[2]int{3, 4}}}}], len(gm))

	// interface keys holding distinct concrete types, including an array.
	im := map[interface{}]int{}
	im[1] = 10
	im["1"] = 20
	im[[2]int{1, 1}] = 30
	fmt.Println(im[1], im["1"], im[[2]int{1, 1}], len(im))

	// pointer keys keep identity.
	type T struct{ V int }
	a, b := &T{1}, &T{1}
	pm := map[*T]string{a: "a", b: "b"}
	fmt.Println(pm[a], pm[b], len(pm))

	// delete by an equal-by-value array key.
	delete(am, [2]int{1, 1})
	fmt.Println(len(am))
}
