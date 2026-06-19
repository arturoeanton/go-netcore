package main

func main() {
	s := "áb€λ"
	println(len(s))
	for i, r := range s {
		println(i, r)
	}
	count := 0
	for range s {
		count++
	}
	println(count)
}
