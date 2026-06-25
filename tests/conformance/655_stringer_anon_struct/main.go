package main

import "fmt"

// A Stringer type used as a field of an ANONYMOUS struct must still have String() invoked by
// fmt — same as a named struct field. The anonymous struct gets a field-type identity entry.
type Stringy int

func (s Stringy) String() string { return fmt.Sprintf("S(%d)", int(s)) }

type Level int

func (l Level) String() string { return []string{"low", "mid", "high"}[l] }

func main() {
	// anonymous struct, value and %+v
	fmt.Printf("%v %+v\n", struct {
		S Stringy
		N int
	}{5, 9}, struct {
		S Stringy
		N int
	}{5, 9})

	// anonymous struct with two Stringer fields and a plain one
	v := struct {
		A Stringy
		B Level
		C string
	}{2, 1, "x"}
	fmt.Println(v)
	fmt.Printf("%+v\n", v)

	// anonymous struct in a slice
	fmt.Println([]struct{ S Stringy }{{0}, {3}})

	// nested anonymous struct
	fmt.Printf("%v\n", struct {
		In struct{ S Stringy }
		T  Level
	}{struct{ S Stringy }{7}, 0})
}
