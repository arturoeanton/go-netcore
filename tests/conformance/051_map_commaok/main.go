package main

func main() {
	m := map[string]int{"x": 10, "y": 20}
	v, ok := m["x"]
	println(v, ok)
	w, ok2 := m["z"]
	println(w, ok2)
	if _, present := m["y"]; present {
		println("has y")
	}
	m["z"] = 99
	v2, ok3 := m["z"]
	println(v2, ok3)
}
