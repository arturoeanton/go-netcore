package main

import (
	"fmt"
	"sync"
)

// A variadic function called through a func value, a `go` statement, or a bound
// method value packs its trailing arguments into the variadic slice (the direct-call
// path always did; these paths previously passed the args unpacked).
func variadic(xs ...string) string {
	return fmt.Sprintf("%d:%v", len(xs), xs)
}

func mixed(label string, ns ...int) string {
	t := 0
	for _, n := range ns {
		t += n
	}
	return fmt.Sprintf("%s=%d", label, t)
}

type counter struct{ name string }

func (c *counter) add(ns ...int) string {
	t := 0
	for _, n := range ns {
		t += n
	}
	return fmt.Sprintf("%s:%d", c.name, t)
}

func main() {
	// func value
	f := variadic
	fmt.Println(f("a"), f("a", "b"), f())
	g := mixed
	fmt.Println(g("x", 1, 2, 3), g("y"))

	// bound method value
	c := &counter{"c"}
	m := c.add
	fmt.Println(m(10, 20), m())

	// spread through a func value
	args := []string{"p", "q", "r"}
	fmt.Println(f(args...))

	// go statement (collect results deterministically)
	var wg sync.WaitGroup
	out := make([]string, 4)
	wg.Add(4)
	go func() { defer wg.Done(); out[0] = variadic("g1", "g2") }()
	go func() { defer wg.Done(); out[1] = f("h1") }()
	go func() { defer wg.Done(); out[2] = mixed("z", 5, 6) }()
	go func() { defer wg.Done(); out[3] = c.add(7, 8, 9) }()
	wg.Wait()
	for _, s := range out {
		fmt.Println(s)
	}
}
