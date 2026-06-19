package main

func sum(nums ...int) int {
	total := 0
	for _, n := range nums {
		total += n
	}
	return total
}

func sumWith(label string, nums ...int) int {
	s := sum(nums...)
	println(label, s)
	return s
}

func main() {
	println(sum())
	println(sum(1, 2, 3))
	println(sum(10, 20, 30, 40))
	sumWith("total", 5, 5, 5)
	xs := []int{1, 2, 3, 4}
	println(sum(xs...))
}
