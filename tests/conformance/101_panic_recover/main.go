package main

func recoverer() {
	if r := recover(); r != nil {
		println("recovered:", r.(string))
	}
}

func safe() {
	defer recoverer()
	println("before")
	panic("kaboom")
}

func main() {
	safe()
	println("after safe")
}
