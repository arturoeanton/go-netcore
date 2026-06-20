package main

import "fmt"

type Animal interface{ Sound() string }
type Named interface {
	Animal
	Name() string
}

type Dog struct{ n string }

func (d Dog) Sound() string { return "woof" }
func (d Dog) Name() string  { return d.n }

type Rock struct{}

func (Rock) Sound() string { return "..." }

func describe(a Animal) {
	// interface-to-interface assertion: only Dog also implements Named
	if n, ok := a.(Named); ok {
		fmt.Printf("%s says %s\n", n.Name(), n.Sound())
	} else {
		fmt.Printf("anonymous: %s\n", a.Sound())
	}
}

func main() {
	for _, a := range []Animal{Dog{n: "Rex"}, Rock{}} {
		describe(a)
	}
	// error interface assertion against a runtime error
	var e error = fmt.Errorf("bad: %d", 7)
	if _, ok := e.(interface{ Error() string }); ok {
		fmt.Println("implements error")
	}
}
