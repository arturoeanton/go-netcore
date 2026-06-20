package main

import "fmt"

// Reslicing into the capacity region (s[len:cap]) must read the element zero value,
// not a garbage/nil — for both make(cap) and append-grown backings.
func markedLast(names []string) bool {
	return cap(names) > len(names) && names[cap(names)-1:cap(names)][0] == "MARK"
}

func main() {
	a := make([]string, 1, 4)
	a[0] = "x"
	fmt.Println(markedLast(a), a[len(a):cap(a)])

	var b []string
	for i := 0; i < 5; i++ {
		b = append(b, fmt.Sprintf("v%d", i))
	}
	// grown by append; the tail capacity must be empty strings
	tail := b[len(b):cap(b)]
	fmt.Println(len(tail) >= 0, markedLast(b))

	ints := make([]int, 0, 3)
	ints = append(ints, 7)
	fmt.Println(ints[len(ints):cap(ints)])
}
