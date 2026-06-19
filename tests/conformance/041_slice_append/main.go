package main

func main() {
	var s []int
	for i := 0; i < 5; i++ {
		s = append(s, i*i)
	}
	println(len(s))
	for _, v := range s {
		println(v)
	}
	t := append(s, 100, 200)
	println(len(t), t[5], t[6])
}
