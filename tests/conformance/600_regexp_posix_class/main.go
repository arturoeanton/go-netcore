package main

import (
	"fmt"
	"regexp"
)

// POSIX character classes ([[:digit:]], [[:alpha:]], …) are RE2 syntax; goclr expands
// them to ASCII ranges for the .NET engine.
func main() {
	cases := []struct{ pat, input string }{
		{`[[:digit:]]+`, "abc123def456"},
		{`[[:alpha:]]+`, "abc123def"},
		{`[[:alnum:]]+`, "a1!b2@c3"},
		{`[[:upper:]]+`, "abcDEFghi"},
		{`[[:lower:]]+`, "ABCdefGHI"},
		{`[[:space:]]+`, "a   b\tc\nd"},
		{`[[:punct:]]+`, "a!@#b"},
		{`[[:xdigit:]]+`, "xyzFF00zz"},
		{`[[:blank:]]+`, "a \t b"},
		{`[[:word:]]+`, "a_b-c"},
		{`[[:alpha:][:digit:]]+`, "ab12!cd34"},
		{`[^[:digit:]]+`, "123abc456"},
		{`([[:upper:]][[:lower:]]+)`, "HelloWorld"},
		{`[[:graph:]]+`, "ab cd"},
	}
	for _, c := range cases {
		re := regexp.MustCompile(c.pat)
		fmt.Printf("%-24s -> %q all=%v\n", c.pat, re.FindString(c.input), re.FindAllString(c.input, -1))
	}

	// POSIX class used in a replacement scan
	re := regexp.MustCompile(`[[:space:]]+`)
	fmt.Printf("%q\n", re.ReplaceAllString("a  b\t c", "_"))

	// MatchString with anchors + POSIX
	fmt.Println(regexp.MustCompile(`^[[:alnum:]]+$`).MatchString("abc123"))
	fmt.Println(regexp.MustCompile(`^[[:alnum:]]+$`).MatchString("abc 123"))
}
