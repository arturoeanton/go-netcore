package main

import "fmt"

// Assigning to an element through a pointer-to-array — a[i] = v where a is *[N]T —
// auto-derefs in Go and writes the shared backing. Used by hash permutations
// (keccakF1600 takes a *[25]uint64 and writes a[0], a[6], ...).
func scramble(a *[5]uint64) {
	for i := 0; i < len(a); i++ {
		a[i] = a[i]*2 + uint64(i)
	}
	a[0] = a[0] ^ a[4]
}

func main() {
	arr := [5]uint64{1, 2, 3, 4, 5}
	scramble(&arr)
	fmt.Println(arr)
}
