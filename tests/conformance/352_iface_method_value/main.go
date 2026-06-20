package main

import "fmt"

type Greeter interface{ Greet(name string) string }

type En struct{ p string }

func (e En) Greet(n string) string { return e.p + " " + n }

type Es struct{}

func (Es) Greet(n string) string { return "Hola " + n }

func apply(f func(string) string, x string) string { return f(x) }

func main() {
	var g Greeter = En{"Hello"}
	f := g.Greet
	fmt.Println(f("Ana"))
	g = Es{}
	fmt.Println(apply(g.Greet, "Bob"))
}
