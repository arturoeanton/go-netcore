package main

func Min[T int | float64](a, b T) T {
	if a < b {
		return a
	}
	return b
}

func Map[T any, U any](xs []T, f func(T) U) []U {
	out := make([]U, 0, len(xs))
	for _, x := range xs {
		out = append(out, f(x))
	}
	return out
}

func Sum[T int | int64](xs []T) T {
	var total T
	for _, x := range xs {
		total += x
	}
	return total
}

func Keys[K comparable, V any](m map[K]V) int {
	n := 0
	for range m {
		n++
	}
	return n
}

func main() {
	println(Min(3, 5))
	println(Min(10, 2))
	println(int(Min(2.5, 1.5) * 10))

	nums := []int{1, 2, 3, 4}
	doubled := Map(nums, func(x int) int { return x * 2 })
	println(doubled[0], doubled[3])
	println(Sum(nums))

	strs := Map(nums, func(x int) string {
		if x%2 == 0 {
			return "even"
		}
		return "odd"
	})
	println(strs[0], strs[1])

	m := map[string]int{"a": 1, "b": 2, "c": 3}
	println(Keys(m))
}
