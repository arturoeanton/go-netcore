package main

type Name struct{ First, Last string }
type Person struct {
	Name Name
	Age  int
}

func main() {
	p := Person{Name: Name{First: "Ada", Last: "Lovelace"}, Age: 36}
	println(p.Name.First)
	println(p.Name.Last)
	println(p.Age)
	p.Age = 37
	println(p.Age)
	p.Name.First = "Augusta"
	println(p.Name.First)
}
