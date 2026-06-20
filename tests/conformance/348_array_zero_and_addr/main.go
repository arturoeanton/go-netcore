package main

import "fmt"

var flags [4]bool
var counts [3]int

func main() {
	// zero-valued fixed arrays have full length (not nil)
	var local [5]int
	local[2] = 9
	fmt.Println(local, len(local))

	// global array mutation (zero value is full-length)
	flags[1] = true
	flags[3] = true
	fmt.Println(flags[0], flags[1], flags[2], flags[3])
	counts[0] = 10
	counts[2] = 30
	fmt.Println(counts)
}
