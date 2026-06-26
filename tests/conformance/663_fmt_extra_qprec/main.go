package main

import "fmt"

// fmt reports leftover arguments as %!(EXTRA type=val, ...) (suppressed once an explicit
// argument index [n] reorders args), and %q on a string honors precision by truncating to
// that many runes before quoting.
func main() {
	fmt.Println(fmt.Sprintf("%d %d", 1))      // missing arg
	fmt.Println(fmt.Sprintf("%d", 1, 2))      // one extra
	fmt.Println(fmt.Sprintf("%d", 1, 2, "x")) // multiple extra, mixed types
	fmt.Println(fmt.Sprintf("%d %d", 1, 2, 3))
	fmt.Println(fmt.Sprintf("%[1]d", 1, 2)) // reordered -> no EXTRA
	fmt.Println(fmt.Sprintf("plain", 7))    // extra with no verbs
	fmt.Println(fmt.Sprintf("%d %d", 1, 2)) // exact, no EXTRA

	fmt.Printf("[%.3q][%.0q][%.10q]\n", "hello", "hi", "hi")
	fmt.Printf("[%10.3q][%-10.3q]\n", "hello", "hello")
	fmt.Printf("%.2q\n", "héllo")
}
