package main

import "fmt"

// Assigning/modifying a struct field through an explicit pointer dereference — (*p).field = v,
// (*p).field++, (*p).field += v, and (**pp).field — must behave like the auto-deref p.field form.
// Previously (*p).field as an lvalue was rejected ("addressable expression").
type P struct {
	X, Y int
	S    []int
}

func main() {
	p := &P{1, 2, []int{10}}

	(*p).Y = 20
	fmt.Println(*p)

	(*p).X++
	(*p).Y--
	fmt.Println(*p)

	(*p).X += 5
	(*p).Y *= 3
	fmt.Println(*p)

	// rvalue read through an explicit deref
	v := (*p).X
	fmt.Println("read:", v)

	// field of a composite type
	(*p).S = append((*p).S, 20, 30)
	fmt.Println((*p).S)

	// double pointer
	pp := &p
	(**pp).X = 100
	(**pp).Y = 200
	fmt.Println(*p)

	// parenthesized auto-deref still works
	(p).X = 7
	fmt.Println(p.X)
}
