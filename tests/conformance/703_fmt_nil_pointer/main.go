package main

import "fmt"

type Point struct{ X, Y int }

// Regression for fmt of nil interfaces and typed nil pointers.
//
//   - An untyped nil interface prints "<nil>" for %v/%T and "%!verb(<nil>)" for
//     every other verb (no type=value tail).
//   - A typed nil pointer (or func/chan) follows Go's fmtPointer: %v -> "<nil>",
//     %T -> the type name, %p -> "0x0", the integer verbs b/o/d/x/X -> the zero
//     address "0" (honoring the # flag), and any other verb -> "%!verb(T=<nil>)".
func main() {
	// Untyped nil interface across the verb set.
	for _, verb := range []string{"%v", "%s", "%d", "%x", "%X", "%o", "%b", "%c", "%U", "%t", "%f", "%g", "%e", "%q", "%p", "%T"} {
		fmt.Printf("%-4s => "+verb+"\n", verb, nil)
	}

	// Typed nil pointers: integer verbs print 0, others bad-verb with the *T name.
	var ip *int
	fmt.Printf("[%v][%d][%x][%#x][%o][%b][%p][%T]\n", ip, ip, ip, ip, ip, ip, ip, ip)
	fmt.Printf("[%s][%f][%g][%q][%c]\n", ip, ip, ip, ip, ip)
	fmt.Printf("[%#v][%+v]\n", ip, ip)

	var sp *string
	fmt.Printf("[%v][%d][%s][%T]\n", sp, sp, sp, sp)

	var fp *Point
	fmt.Printf("[%v][%d][%T][%#v]\n", fp, fp, fp, fp)

	var bp *bool
	fmt.Printf("[%v][%d][%t][%T]\n", bp, bp, bp, bp)

	var dp *float64
	fmt.Printf("[%v][%d][%g][%T]\n", dp, dp, dp, dp)

	// Mixed: a live pointer alongside a nil one (only the nil is byte-stable).
	x := 7
	live := &x
	fmt.Printf("live=%v nil=%v nild=%d\n", *live, ip, ip)
}
