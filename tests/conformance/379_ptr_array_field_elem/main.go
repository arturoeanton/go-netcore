package main

import "fmt"

// Field assignment to, and address-of, an element of a pointer-to-array (*[N]T).
// Go auto-derefs the pointer to the array. Used by x/net/http2/hpack's huffman table.
type node struct {
	sym     byte
	codeLen int
}

func main() {
	leaves := new([4]node)
	for i := 0; i < 4; i++ {
		leaves[i].sym = byte(i * 10)
		leaves[i].codeLen = i
	}
	p := &leaves[2]
	p.sym = 99
	fmt.Println(leaves[0].sym, leaves[2].sym, leaves[3].codeLen)
}
