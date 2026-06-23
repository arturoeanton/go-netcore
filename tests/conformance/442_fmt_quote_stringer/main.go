package main

import "fmt"

// %q on a value implementing error or Stringer quotes its Error()/String()
// result, just as %s/%v invoke the method.
type myErr struct{ msg string }

func (e myErr) Error() string { return e.msg }

type color int

func (c color) String() string {
	switch c {
	case 0:
		return "red"
	default:
		return "blue"
	}
}

func main() {
	var err error = myErr{"boom \"x\""}
	fmt.Printf("%s|%v|%q\n", err, err, err)

	var c color = 0
	fmt.Printf("%s|%v|%q\n", c, c, c)
	fmt.Printf("%q\n", color(1))

	// a named integer WITHOUT a Stringer still rune-quotes under %q
	type rawint int
	fmt.Printf("%q\n", rawint(65))
}
