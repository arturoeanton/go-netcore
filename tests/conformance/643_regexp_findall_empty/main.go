package main

import (
	"fmt"
	"regexp"
)

// FindAll*/ReplaceAll* follow Go's empty-match rule: an empty match sitting exactly at the
// previous match's end is dropped (unlike .NET Regex.Matches/Replace which keep it). This
// covers the string, byte, submatch, index, and replace families with empty-capable patterns
// (`a*`, `\s*`, ``) plus non-empty patterns (`\w+`, capture groups) for no-regression.
func main() {
	a := regexp.MustCompile(`a*`)
	fmt.Printf("%q\n", a.FindAllString("baaab", -1))
	fmt.Printf("%v\n", a.FindAllStringIndex("baaab", -1))
	fmt.Printf("%q\n", a.ReplaceAllString("baaab", "-"))
	fmt.Printf("%q\n", a.ReplaceAllLiteralString("baaab", "[$0]"))
	fmt.Printf("%q\n", a.ReplaceAllStringFunc("baaab", func(s string) string { return fmt.Sprintf("<%d>", len(s)) }))
	fmt.Printf("%q\n", a.FindAll([]byte("baaab"), -1))
	fmt.Printf("%v\n", a.FindAllIndex([]byte("baaab"), -1))
	fmt.Printf("%q\n", a.FindAllString("baaab", 2)) // limit

	ws := regexp.MustCompile(`\s*`)
	fmt.Printf("%q\n", ws.FindAllString("a b  c", -1))
	fmt.Printf("%q\n", ws.ReplaceAllString("a b  c", "_"))

	empty := regexp.MustCompile(``)
	fmt.Printf("%q\n", empty.FindAllString("abc", -1))
	fmt.Printf("%q\n", empty.ReplaceAllString("abc", "-"))

	word := regexp.MustCompile(`\w+`)
	fmt.Printf("%q\n", word.FindAllString("Hello, World!", -1))
	fmt.Printf("%v\n", word.FindAllStringIndex("Hello, World!", -1))

	sub := regexp.MustCompile(`(\w)(\d)`)
	fmt.Printf("%q\n", sub.FindAllStringSubmatch("a1b2c3", -1))
	fmt.Printf("%v\n", sub.FindAllStringSubmatchIndex("a1b2c3", -1))
	fmt.Printf("%q\n", sub.ReplaceAllString("a1b2", "$2$1"))
	fmt.Printf("%v\n", sub.FindAllSubmatch([]byte("a1b2"), -1))
	fmt.Printf("%v\n", sub.FindAllSubmatchIndex([]byte("a1b2"), -1))
}
