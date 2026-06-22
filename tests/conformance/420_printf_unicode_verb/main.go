package main

import "fmt"

// Exercises the %U / %#U Printf verbs (Unicode code point as U+XXXX), including
// the '#'-flag form that appends the quoted character when it is printable.
func main() {
	fmt.Printf("%U %U %U\n", 'a', '中', 0x1F600)
	fmt.Printf("%#U %#U %#U\n", 'a', '中', rune(7)) // bell is not printable: no char
	fmt.Printf("%U\n", rune(0))
	for _, r := range "héllo, 世界" {
		fmt.Printf("%U ", r)
	}
	fmt.Println()
}
