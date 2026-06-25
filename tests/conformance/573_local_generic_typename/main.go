package main

import "fmt"

// %T of a local struct type declared inside a generic function: Go names it with the
// enclosing function's type arguments in declaration order.
func boxOf[T any](x T) any {
	type box struct{ v T }
	return box{v: x}
}

func entryOf[K comparable, V any](k K, v V) any {
	type entry struct {
		k K
		v V
	}
	return entry{k, v}
}

func main() {
	fmt.Printf("%T\n", boxOf("hi"))
	fmt.Printf("%T\n", boxOf(42))
	fmt.Printf("%T\n", boxOf(3.14))
	fmt.Printf("%T\n", entryOf("a", 1))
	fmt.Printf("%T\n", entryOf(1, "b"))
	fmt.Printf("%T\n", entryOf(true, 2.5))

	// The value still formats correctly alongside its type.
	fmt.Printf("%T=%v\n", boxOf("z"), boxOf("z"))
}
