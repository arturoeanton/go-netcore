package main

import "fmt"

func main() {
	// multi-result function value
	divmod := func(a, b int) (int, int) { return a / b, a % b }
	q, r := divmod(17, 5)
	fmt.Println(q, r)

	// with an error result and capture
	limit := 100
	checked := func(n int) (int, error) {
		if n > limit {
			return 0, fmt.Errorf("over limit %d", limit)
		}
		return n * 2, nil
	}
	v, err := checked(40)
	fmt.Println(v, err)
	_, err2 := checked(200)
	fmt.Println(err2)

	// stored in a variable, called later, three results
	stats := func(xs []int) (int, int, int) {
		sum, lo, hi := 0, xs[0], xs[0]
		for _, x := range xs {
			sum += x
			if x < lo {
				lo = x
			}
			if x > hi {
				hi = x
			}
		}
		return sum, lo, hi
	}
	s, mn, mx := stats([]int{3, 1, 4, 1, 5, 9, 2})
	fmt.Println(s, mn, mx)

	// discarding one result
	first := func() (string, bool) { return "ok", true }
	name, _ := first()
	fmt.Println(name)
}
