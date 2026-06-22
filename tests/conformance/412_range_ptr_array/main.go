package main

import "fmt"

func sum(p *[4]int) int {
	t := 0
	for i, v := range p {
		t += i * v
	}
	return t
}

func main() {
	a := [4]int{10, 20, 30, 40}
	fmt.Println(sum(&a))

	b := new([3]uint16)
	b[0] = 5
	b[1] = 7
	b[2] = 9
	var s uint16
	for _, v := range b {
		s += v
	}
	fmt.Println(s)

	for i := range b {
		b[i] = uint16(i * i)
	}
	fmt.Println(b[0], b[1], b[2])
}
