package main

import (
	"fmt"
	"slices"
)

// A user-defined iter.Seq.
func countTo(n int) func(func(int) bool) {
	return func(yield func(int) bool) {
		for i := 0; i < n; i++ {
			if !yield(i) {
				return
			}
		}
	}
}

// A user-defined iter.Seq2.
func enumerate(xs []string) func(func(int, string) bool) {
	return func(yield func(int, string) bool) {
		for i, x := range xs {
			if !yield(i, x) {
				return
			}
		}
	}
}

func main() {
	// Plain iteration.
	for v := range countTo(4) {
		fmt.Print(v, " ")
	}
	fmt.Println()

	// break stops the iterator.
	total := 0
	for v := range countTo(100) {
		if v == 5 {
			break
		}
		total += v
	}
	fmt.Println("total", total)

	// continue skips.
	odd := 0
	for v := range countTo(10) {
		if v%2 == 0 {
			continue
		}
		odd += v
	}
	fmt.Println("odd", odd)

	// Capture and mutate an outer variable.
	collected := []int{}
	factor := 3
	for v := range countTo(4) {
		collected = append(collected, v*factor)
	}
	fmt.Println("collected", collected)

	// Seq2 with both variables.
	for i, s := range enumerate([]string{"a", "b", "c"}) {
		fmt.Printf("%d:%s ", i, s)
	}
	fmt.Println()

	// Standard-library iterators consumed directly.
	for i, v := range slices.All([]int{10, 20, 30}) {
		fmt.Printf("[%d]=%d ", i, v)
	}
	fmt.Println()
	for v := range slices.Values([]string{"x", "y", "z"}) {
		fmt.Print(v)
	}
	fmt.Println()

	// Nested ordinary loop inside the body keeps its own break/continue.
	for v := range countTo(2) {
		for j := 0; j < 4; j++ {
			if j == 2 {
				break
			}
			fmt.Printf("(%d,%d)", v, j)
		}
	}
	fmt.Println()
}
