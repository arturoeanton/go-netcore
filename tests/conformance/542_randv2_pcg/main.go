package main

import (
	"fmt"
	"math/rand/v2"
)

func main() {
	// PCG directly.
	p := rand.NewPCG(1, 2)
	for i := 0; i < 4; i++ {
		fmt.Println(p.Uint64())
	}

	// rand.New(PCG) — typed/bounded draws.
	r := rand.New(rand.NewPCG(42, 1024))
	fmt.Println("u64", r.Uint64())
	fmt.Println("i64", r.Int64())
	fmt.Println("u32", r.Uint32())
	fmt.Println("i32", r.Int32())
	fmt.Println("int", r.Int())
	fmt.Println("u64n", r.Uint64N(1000000007))
	fmt.Println("i64n", r.Int64N(1<<40))
	fmt.Println("u32n", r.Uint32N(7))
	fmt.Println("i32n", r.Int32N(100))
	fmt.Println("intn", r.IntN(999983))
	fmt.Printf("f64=%.17g f32=%.9g\n", r.Float64(), r.Float32())

	// A pile of bounded draws (exercises power-of-two and Lemire paths).
	r2 := rand.New(rand.NewPCG(7, 7))
	sum := 0
	for i := 0; i < 10000; i++ {
		sum += r2.IntN(int(r2.Uint32N(64)) + 1)
	}
	fmt.Println("sum", sum)

	// Seed + MarshalBinary round-trip.
	p2 := rand.NewPCG(0, 0)
	p2.Seed(123, 456)
	b, _ := p2.MarshalBinary()
	fmt.Printf("marshal %x\n", b)
	p3 := rand.NewPCG(0, 0)
	p3.UnmarshalBinary(b)
	fmt.Println("restored eq", p2.Uint64() == p3.Uint64())
}
