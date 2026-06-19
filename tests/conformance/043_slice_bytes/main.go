package main

func main() {
	s := "héllo"
	b := []byte(s)
	println(len(b))
	for _, x := range b {
		println(x)
	}
	r := []rune(s)
	println(len(r))
	for _, c := range r {
		println(c)
	}
}
