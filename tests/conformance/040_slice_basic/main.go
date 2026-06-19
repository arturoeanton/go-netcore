package main

func main() {
	s := make([]int, 3)
	s[0] = 10
	s[1] = 20
	s[2] = 30
	println(len(s), cap(s))
	println(s[0], s[1], s[2])
	sum := 0
	for i := 0; i < len(s); i++ {
		sum += s[i]
	}
	println(sum)
}
