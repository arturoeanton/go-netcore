package main

import "fmt"

// A value-receiver String()/Error() is in *T's method set too, so fmt of a *T to a named
// (non-struct) Stringer dispatches the method — Go's &money prints "$12.50", not the address.
// Previously goclr printed the raw pointer address for such pointers.
type Money int

func (m Money) String() string { return fmt.Sprintf("$%d.%02d", m/100, m%100) }

type MyErr int

func (e MyErr) Error() string { return fmt.Sprintf("err#%d", int(e)) }

type Pt struct{ X int }

func (p Pt) String() string { return fmt.Sprintf("Pt(%d)", p.X) }

func main() {
	m := Money(1250)
	fmt.Println(&m)               // $12.50
	fmt.Printf("%v %s\n", &m, &m) // $12.50 $12.50

	e := MyErr(7)
	fmt.Println(&e)    // err#7
	var err error = &e // *MyErr satisfies error (value-receiver Error)
	fmt.Println(err)

	// The value itself still prints via String() (no regression).
	fmt.Println(m, e.Error())

	// Pointer to a struct Stringer still works.
	p := Pt{5}
	fmt.Println(&p) // Pt(5)

	// Pointer-to-stringer inside a slice.
	fmt.Println([]*Money{&m}) // [$12.50]
}
