package main

import (
	"fmt"
	"regexp"
)

// Go numbers capturing groups left-to-right by opening paren; .NET orders unnamed
// groups before named ones. Patterns mixing the two must still report Go's order.
func main() {
	re := regexp.MustCompile(`(?i)(?P<word>\w+)\s+(\d+)`)
	fmt.Println(re.FindAllStringSubmatch("Foo 12 bar 34", -1))
	fmt.Println(re.FindStringSubmatch("Foo 12"))
	fmt.Println(re.SubexpNames())
	fmt.Println(re.SubexpIndex("word"))
	fmt.Println(re.ReplaceAllString("Foo 12", "$2=$word"))
	fmt.Println(re.ReplaceAllString("Foo 12", "${word}-${2}"))
	fmt.Println(re.ReplaceAllString("Foo 12", "$nope|$$|done"))

	// Submatch byte indices in Go order.
	fmt.Println(re.FindStringSubmatchIndex("Foo 12"))

	// Purely numbered groups (regression — order must be unchanged).
	re2 := regexp.MustCompile(`(\w+)\s+(\d+)`)
	fmt.Println(re2.FindStringSubmatch("Foo 12"))
	fmt.Println(re2.ReplaceAllString("Foo 12", "$2-$1"))

	// All-named, multiple groups.
	re3 := regexp.MustCompile(`(?P<a>\w+)-(?P<b>\w+)`)
	fmt.Println(re3.FindStringSubmatch("x-y"), re3.SubexpNames())

	// Named between unnamed: unnamed, named, unnamed.
	re4 := regexp.MustCompile(`(\d+)(?P<mid>[a-z]+)(\d+)`)
	fmt.Println(re4.FindStringSubmatch("12abc34"))
	fmt.Println(re4.SubexpNames())
	fmt.Println(re4.ReplaceAllString("12abc34", "$3-$mid-$1"))

	// Expand with named refs.
	src := "Foo 12"
	m := re.FindStringSubmatchIndex(src)
	dst := re.ExpandString(nil, "[$word/$2]", src, m)
	fmt.Println(string(dst))
}
