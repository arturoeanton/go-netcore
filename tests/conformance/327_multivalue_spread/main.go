package main

import "fmt"

func pair() (int, string)      { return 7, "x" }
func three() (int, int, int)   { return 1, 2, 3 }
func combine(a int, b string) string { return fmt.Sprintf("%d:%s", a, b) }
func sum3(a, b, c int) int     { return a + b + c }

func main() {
	// f(g()) exact-match spread
	fmt.Println(combine(pair()))
	fmt.Println(sum3(three()))

	// spread into a variadic (fmt.Println(...any))
	fmt.Println(pair())
	fmt.Println(three())
}
