package main

import "fmt"

// A closure (func literal) whose result type is an interface must tag the returned
// composite/named value with its dynamic type, so a type assertion or type switch on the
// result works — exactly as a named function already did. Previously closures skipped the
// tagging, so `func() any { return make([]byte, n) }()` asserted as []interface{}.
type Celsius float64

func main() {
	mk := func() any { return make([]byte, 3) }
	fmt.Println(mk().([]byte))

	mkInts := func() any { return []int{1, 2, 3} }
	fmt.Println(mkInts().([]int))

	mkC := func() any { return Celsius(36.6) }
	fmt.Printf("%.1f\n", float64(mkC().(Celsius)))

	// type switch on closure-returned interface values
	fns := []func() any{
		func() any { return []byte{1, 2} },
		func() any { return []string{"x"} },
		func() any { return 42 },
	}
	for _, f := range fns {
		switch t := f().(type) {
		case []byte:
			fmt.Println("bytes", t)
		case []string:
			fmt.Println("strings", t)
		default:
			fmt.Printf("other %T %v\n", t, t)
		}
	}
}
