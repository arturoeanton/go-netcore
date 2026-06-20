package main

import "fmt"

func devirt(s string) (string, []byte) {
	if len(s) > 3 {
		return "", []byte(s)
	}
	return s, nil
}

func main() {
	a, u := devirt("hi")
	fmt.Println(a, u == nil, len(u))
	b, v := devirt("hello")
	fmt.Println(b, v == nil, string(v))
}
