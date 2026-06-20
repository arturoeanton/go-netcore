package main

import (
	"fmt"
	"sort"
	"time"
)

func main() {
	a := []int{5, 2, 8, 1, 9, 3}
	sort.Ints(a)
	fmt.Println(a)
	println(sort.IntsAreSorted(a), sort.SearchInts(a, 8))
	s := []string{"banana", "apple", "cherry"}
	sort.Strings(s)
	fmt.Println(s)
	d := 90 * time.Minute
	println(int(d.Hours()), int(d.Minutes()), d.String())
	println((1500 * time.Millisecond).String())
}
