package main

import (
	"fmt"
	"math/rand"
)

func main() {
	r := rand.New(rand.NewSource(42))
	for i := 0; i < 5; i++ {
		fmt.Println(r.Intn(100))
	}
	fmt.Println(r.Int63())
	fmt.Println(r.Float64())
	fmt.Println(r.Perm(8))

	r2 := rand.New(rand.NewSource(1))
	sum := 0
	for i := 0; i < 1000; i++ {
		sum += r2.Intn(6) + 1
	}
	fmt.Println("dice sum:", sum)

	r3 := rand.New(rand.NewSource(99))
	fmt.Println(r3.Int63n(1 << 40))
	fmt.Printf("%.6f\n", r3.Float64())
}
