package main

import "errors"

func div(a, b int) (int, error) {
	if b == 0 {
		return 0, errors.New("division by zero")
	}
	return a / b, nil
}

func main() {
	q, err := div(10, 2)
	if err != nil { println("err:", err.Error()) } else { println("q:", q) }
	q, err = div(1, 0)
	if err != nil { println("err:", err.Error()) } else { println("q:", q) }
	e := errors.New("boom")
	println(e.Error(), e == nil)
}
