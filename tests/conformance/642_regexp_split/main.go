package main

import (
	"fmt"
	"regexp"
)

// (*Regexp).Split: with limit n the result has at most n elements and the last is the
// unsplit remainder; n==0 returns nil; an empty-match-capable pattern (`\s*`, `a*`, ``)
// produces no spurious empty fields (Go's empty-match-adjacent-to-previous-end rule).
func main() {
	comma := regexp.MustCompile(`,`)
	for _, n := range []int{-1, 0, 1, 2, 3, 5} {
		fmt.Printf("n=%d: %q\n", n, comma.Split("a,b,c,d", n))
	}

	// leading / trailing / consecutive delimiters
	fmt.Printf("%q\n", comma.Split(",a,,b,", -1))
	fmt.Printf("%q\n", comma.Split(",,,", -1))
	fmt.Printf("%q\n", comma.Split("", -1))
	fmt.Printf("%q\n", comma.Split("nodelim", -1))

	// empty-match-capable patterns
	fmt.Printf("%q\n", regexp.MustCompile(``).Split("abc", -1))
	fmt.Printf("%q\n", regexp.MustCompile(`a*`).Split("xaaxax", -1))
	fmt.Printf("%q\n", regexp.MustCompile(`\s+`).Split("  a  b  c  ", -1))
	fmt.Printf("%q\n", regexp.MustCompile(`\s*`).Split("a b  c", -1))
	fmt.Printf("%q\n", regexp.MustCompile(`\s*`).Split("a b c", 2))

	// non-capturing word splitter
	fmt.Printf("%q\n", regexp.MustCompile(`\W+`).Split("Hello, World! Foo-Bar", -1))

	// multibyte delimiters and content
	fmt.Printf("%q\n", regexp.MustCompile(`、`).Split("世、界、x", -1))
	fmt.Printf("%q\n", regexp.MustCompile(`,`).Split("世,界", -1))
}
