package main

import (
	"fmt"
	"regexp"
	"strings"
)

func main() {
	// ReplaceAllStringFunc: each match transformed by the callback.
	re := regexp.MustCompile(`\d+`)
	fmt.Println(re.ReplaceAllStringFunc("a1b22c333", func(s string) string { return "[" + s + "]" }))
	fmt.Println(re.ReplaceAllStringFunc("x9y", strings.ToUpper))

	// ReplaceAllLiteralString: replacement used verbatim ($1 is not expanded).
	re2 := regexp.MustCompile(`(\w)(\w)`)
	fmt.Println(re2.ReplaceAllLiteralString("ab cd", "$1-"))
	fmt.Println(re2.ReplaceAllString("ab cd", "$1-"))

	// Go-style named groups (?P<name>...) compile and report via SubexpNames; String()
	// returns the original Go pattern.
	re3 := regexp.MustCompile(`^(?P<y>\d{4})-(?P<m>\d{2})$`)
	fmt.Println(re3.FindStringSubmatch("2026-06"))
	fmt.Println(re3.SubexpNames())
	fmt.Println(re3.String())
}
