package main

import (
	"errors"
	"fmt"
)

// An explicit conversion to an interface type — fmt.Stringer(c), error(e), any(v),
// interface{}(x) — boxes and tags the value just like an implicit assignment.
type Color int

func (c Color) String() string { return fmt.Sprintf("C%d", int(c)) }

type MyErr struct{ m string }

func (e *MyErr) Error() string { return e.m }

func main() {
	// explicit conversion as an expression and as a Printf arg
	s := fmt.Stringer(Color(9))
	fmt.Println(s.String())
	fmt.Printf("%s %v\n", fmt.Stringer(Color(7)), fmt.Stringer(Color(7)))

	// error(...)
	fmt.Println(error(errors.New("boom")))
	fmt.Println(error(&MyErr{"custom"}))
	var ne error = error(nil)
	fmt.Println(ne == nil)

	// any(...) keeps the dynamic type
	for _, a := range []any{any(42), any("str"), any(Color(3)), any(3.14), any(true)} {
		fmt.Printf("%v:%T ", a, a)
	}
	fmt.Println()

	// interface{}(struct) and interface-to-interface
	type P struct{ X int }
	var i interface{} = interface{}(P{5})
	fmt.Printf("%v %T\n", i, i)
	var st fmt.Stringer = Color(1)
	fmt.Println(interface{}(st))

	// conversions inside a slice literal, spread to Println
	items := []interface{}{any(1), any("x"), any(Color(2))}
	fmt.Println(items...)
}
