package main

type Acc struct{ total, count int }

func main() {
	a := Acc{}
	add := func(n int) {
		a.total += n
		a.count++
	}
	for _, v := range []int{5, 10, 15} {
		add(v)
	}
	println(a.total, a.count)

	c := Acc{total: 100}
	read := func() int { return c.total }
	c.total = 200
	println(read())
}
