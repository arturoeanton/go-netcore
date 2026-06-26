package main

import (
	"errors"
	"fmt"
)

func safe(f func()) (err error) {
	defer func() {
		if r := recover(); r != nil {
			err = fmt.Errorf("recovered: %v", r)
		}
	}()
	f()
	return nil
}

type myErr struct{ Code int }

// defer/recover/panic semantics: recover from explicit and runtime panics, LIFO defer order,
// Go 1.22 per-iteration loop-variable capture in deferred closures, nested recover, and
// re-panic propagating to an outer recover.
func main() {
	fmt.Println(safe(func() { panic("boom") }))
	fmt.Println(safe(func() { var s []int; _ = s[5] }))
	fmt.Println(safe(func() { a := 0; _ = 1 / a }))
	fmt.Println(safe(func() { var m map[string]int; m["x"] = 1 }))
	fmt.Println(safe(func() { var p *int; _ = *p }))
	fmt.Println(safe(func() { panic(errors.New("err panic")) }))
	fmt.Println(safe(func() { panic(myErr{42}) }))

	func() {
		for i := 0; i < 3; i++ {
			defer fmt.Print(i, " ")
		}
	}()
	fmt.Println()
	func() {
		for i := 0; i < 3; i++ {
			defer func() { fmt.Print(i, " ") }()
		}
	}()
	fmt.Println()

	fmt.Println(safe(func() {
		defer func() { recover() }()
		panic("inner")
	}))
	fmt.Println(safe(func() {
		defer func() {
			if r := recover(); r != nil {
				panic(fmt.Sprintf("wrapped: %v", r))
			}
		}()
		panic("original")
	}))
}
