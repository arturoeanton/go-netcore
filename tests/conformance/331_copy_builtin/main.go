package main

import "fmt"

func main() {
	dst := make([]int, 3)
	n := copy(dst, []int{1, 2, 3, 4, 5})
	fmt.Println(n, dst)

	short := make([]int, 5)
	n2 := copy(short, []int{7, 8})
	fmt.Println(n2, short)

	b := make([]byte, 5)
	copy(b, "hello world")
	fmt.Println(string(b))

	// overlapping copy (shift)
	s := []int{1, 2, 3, 4, 5}
	copy(s[1:], s)
	fmt.Println(s)

	// copy into a sub-slice
	grid := []int{0, 0, 0, 0}
	copy(grid[1:3], []int{9, 9, 9})
	fmt.Println(grid)
}
