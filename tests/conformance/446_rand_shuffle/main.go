package main

import (
	"fmt"
	"math/rand"
)

// A seeded *rand.Rand produces a sequence byte-identical to Go's, including
// Shuffle (which uses Go's Lemire int31n internally).
func main() {
	r := rand.New(rand.NewSource(42))
	fmt.Println(r.Intn(100), r.Intn(100), r.Intn(100))
	fmt.Println(r.Int63n(1000), r.Int63())
	fmt.Printf("%.6f\n", r.Float64())
	fmt.Println(r.Perm(6))

	s := []int{0, 1, 2, 3, 4, 5, 6, 7, 8, 9}
	r.Shuffle(len(s), func(i, j int) { s[i], s[j] = s[j], s[i] })
	fmt.Println(s)

	r2 := rand.New(rand.NewSource(7))
	letters := []string{"a", "b", "c", "d", "e"}
	r2.Shuffle(len(letters), func(i, j int) { letters[i], letters[j] = letters[j], letters[i] })
	fmt.Println(letters)
}
