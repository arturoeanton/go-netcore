package main

type Animal interface {
	Sound() string
}

type Dog struct{}
type Cat struct{}

func (d Dog) Sound() string { return "woof" }
func (c Cat) Sound() string { return "meow" }

func main() {
	animals := []Animal{Dog{}, Cat{}, Dog{}}
	for _, a := range animals {
		println(a.Sound())
	}
	var x Animal = Cat{}
	switch x.(type) {
	case Dog:
		println("is dog")
	case Cat:
		println("is cat")
	}
}
