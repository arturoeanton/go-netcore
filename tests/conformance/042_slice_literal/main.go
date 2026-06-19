package main

func sum(xs []int) int {
	total := 0
	for _, x := range xs {
		total += x
	}
	return total
}

func main() {
	xs := []int{2, 4, 6, 8}
	println(len(xs))
	println(sum(xs))
	for i, v := range xs {
		println(i, v)
	}
	sub := xs[1:3]
	println(len(sub), sub[0], sub[1])
}
