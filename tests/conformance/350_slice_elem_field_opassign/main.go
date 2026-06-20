package main

import "fmt"

type E struct {
	pc   int
	name string
}

func main() {
	s := []E{{10, "a"}, {20, "b"}, {30, "c"}}
	d := 5
	for i := range s {
		s[i].pc -= d
	}
	fmt.Println(s)
	s[1].pc += 100
	s[2].pc *= 2
	fmt.Println(s[1], s[2])
}
