package main

import (
	"fmt"

	"github.com/arturoeanton/go-netcore/tests/conformance/324_generic_explicit/box"
)

// Same-package generic with explicit type arguments.
func Zero[T any]() T { var z T; return z }
func Repeat[T any](x T, n int) []T {
	out := make([]T, 0, n)
	for i := 0; i < n; i++ {
		out = append(out, x)
	}
	return out
}

func main() {
	// explicit type args, same package
	fmt.Println(Zero[int](), Zero[string]() == "")
	fmt.Println(Repeat[string]("ab", 3))

	// cross-package generic type, explicit instantiation + methods
	b := box.New[int]()
	b.Add(10)
	b.Add(20)
	b.Add(30)
	fmt.Println(b.Len(), b.At(0), b.At(2))

	// cross-package generic function, explicit type args
	doubled := box.Map[int, int]([]int{1, 2, 3}, func(x int) int { return x * 2 })
	fmt.Println(doubled)
	labels := box.Map[int, string]([]int{1, 2}, func(x int) string { return fmt.Sprintf("n%d", x) })
	fmt.Println(labels)
}
