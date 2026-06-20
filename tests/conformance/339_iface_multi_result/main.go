package main

import "fmt"

type Validator interface {
	Check(int) (bool, string)
}

type Positive struct{}

func (Positive) Check(n int) (bool, string) {
	if n > 0 {
		return true, "positive"
	}
	return false, "non-positive"
}

type Even struct{ tag string }

func (e Even) Check(n int) (bool, string) {
	if n%2 == 0 {
		return true, e.tag + ":even"
	}
	return false, e.tag + ":odd"
}

func run(v Validator, n int) {
	ok, msg := v.Check(n)
	fmt.Printf("%d -> %v %q\n", n, ok, msg)
}

func main() {
	vs := []Validator{Positive{}, Even{tag: "E"}}
	for _, v := range vs {
		for _, n := range []int{-2, 3, 4} {
			run(v, n)
		}
	}
	// also exercise the result used directly in a boolean context
	var v Validator = Even{tag: "x"}
	if ok, _ := v.Check(10); ok {
		fmt.Println("10 is even")
	}
}
