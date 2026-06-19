package main

type Stringer interface{ String() string }
type Named struct{ n string }

func (x Named) String() string { return x.n }

func Join[T Stringer](xs []T) string {
	out := ""
	for _, x := range xs {
		out += x.String()
	}
	return out
}

func Map[T, U any](xs []T, f func(T) U) []U {
	r := make([]U, 0, len(xs))
	for _, x := range xs {
		r = append(r, f(x))
	}
	return r
}

func Adder[T int | float64](base T) func(T) T {
	return func(x T) T { return base + x }
}

func main() {
	println(Join([]Named{{"a"}, {"b"}, {"c"}}))
	doubled := Map([]int{1, 2, 3}, func(x int) int { return x * 2 })
	println(doubled[0], doubled[2])
	add10 := Adder(10)
	println(add10(5))
}
