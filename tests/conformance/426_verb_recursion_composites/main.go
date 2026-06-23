package main

import "fmt"

// A scalar verb applied to a slice/array/map recurses element-wise, as Go does.
// %x distinguishes a []byte (hex string) from a []int (recurses).
func main() {
	fmt.Printf("%d\n", []int{1, 2, 3})
	fmt.Printf("%d\n", []int{})
	fmt.Printf("%d\n", [][]int{{1, 2}, {3}})
	fmt.Printf("%d\n", map[int]int{1: 10, 2: 20})
	fmt.Printf("%.1f\n", []float64{1.5, 2.25})
	fmt.Printf("%c\n", []rune{65, 66})
	fmt.Printf("%t\n", []bool{true, false})
	fmt.Printf("%x\n", []int{255, 16})
	fmt.Printf("%x\n", []byte{255, 16})
	fmt.Printf("%X\n", []byte("Go"))
	var s []int
	fmt.Printf("%d\n", s)
}
