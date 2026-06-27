package main

import "fmt"

// %T type-name regression lock: the cases goclr renders byte-exact with go run — basic types,
// named types with a method set, named composites (slice/map), structs, pointers-to-struct, and
// the byte/rune aliases. (Method-less named *scalars/strings* and int64 vs int are a documented
// typed-box gap — see LIMITATIONS — and are intentionally excluded here.)
type Color int

func (c Color) String() string { return "c" }

type IntList []int
type StrMap map[string]int
type Pt struct{ X int }

func main() {
	fmt.Printf("%T %T %T %T\n", 0, "", true, 3.14)
	fmt.Printf("%T %T %T\n", int32(1), uint32(2), uint64(3))
	fmt.Printf("%T\n", Color(1))
	fmt.Printf("%T\n", IntList{1})
	fmt.Printf("%T\n", StrMap{})
	fmt.Printf("%T\n", Pt{})
	fmt.Printf("%T\n", &Pt{})
	fmt.Printf("%T %T\n", []int{}, map[string]int{})
	fmt.Printf("%T %T\n", []byte{1}, []rune{1})
	fmt.Printf("%T\n", []Color{})
	fmt.Printf("%T\n", complex(1, 2))
	fmt.Printf("%T\n", [3]int{})
}
