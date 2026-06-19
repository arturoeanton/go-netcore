package main

func main() {
	s := "ABC"
	println(s[0])
	println(s[1])
	println(len(s))
	sum := 0
	for i := 0; i < len(s); i++ {
		sum += int(s[i])
	}
	println(sum)
}
