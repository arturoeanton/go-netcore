package main

type Animal interface{ Sound() string }

type Dog struct{ name string }

func (d *Dog) Sound() string { return d.name + ": woof" }

type Cat struct{}

func (c Cat) Sound() string { return "meow" }

type myError struct{ msg string }

func (e *myError) Error() string { return "err: " + e.msg }

func main() {
	var a Animal = &Dog{name: "Rex"}
	println(a.Sound())
	var c Animal = Cat{}
	println(c.Sound())
	animals := []Animal{&Dog{name: "Fido"}, Cat{}, &Dog{name: "Spot"}}
	for _, an := range animals {
		println(an.Sound())
	}
	var err error = &myError{msg: "boom"}
	println(err.Error())
}
