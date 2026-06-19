package main

func main() {
	m := make(map[string]int)
	m["a"] = 1
	m["b"] = 2
	m["c"] = 3
	println(len(m))
	println(m["a"], m["b"], m["c"])
	println(m["missing"])
	delete(m, "b")
	println(len(m))
	println(m["b"])
}
