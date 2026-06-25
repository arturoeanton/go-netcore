package main

import "fmt"

// %q and %x recurse into nested slices/maps/structs; an untagged slice (e.g.
// []interface{}) is no longer mistaken for []byte, while a real []byte still formats
// as a quoted/hex string.
type S struct {
	A []string
	N int
}

func main() {
	fmt.Printf("%q\n", [][]string{{"a", "b"}, {"c"}})
	fmt.Printf("%q\n", []string{"x", "y"})
	fmt.Printf("%q\n", [][]int{{65}, {66}})
	fmt.Printf("%q\n", []interface{}{"a", 'b', "c"})
	fmt.Printf("%q\n", []byte("hello"))
	fmt.Printf("%q\n", [][]byte{[]byte("ab"), []byte("cd")})
	fmt.Printf("%q\n", map[string][]string{"k": {"a", "b"}, "j": {"z"}})
	fmt.Printf("%q\n", S{A: []string{"p", "q"}, N: 5})
	fmt.Printf("%q\n", []rune{'a', 'b'})
	fmt.Printf("%q\n", map[string]int(nil))

	fmt.Printf("%x\n", [][]int{{255}, {16}})
	fmt.Printf("%x\n", []byte{0xDE, 0xAD})
	fmt.Printf("%x\n", [][]byte{{0x01}, {0x02}})
	fmt.Printf("%x\n", []interface{}{255, 16})
	fmt.Printf("%x\n", []string{"AB", "cd"})

	fmt.Printf("%d\n", [][]int{{1, 2}, {3}})
	fmt.Printf("%d\n", []interface{}{1, 2, 3})
	fmt.Printf("%v\n", [][]string{{"a"}, {"b"}})
}
