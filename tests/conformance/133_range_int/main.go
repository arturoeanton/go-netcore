package main

func main() {
	// range over an integer literal-ish bound.
	n := 5
	for i := range n {
		println(i)
	}

	// range over an integer with no index variable.
	count := 0
	for range 3 {
		count++
	}
	println("count", count)

	// range over zero iterates not at all.
	for i := range 0 {
		println("never", i)
	}
	println("done")

	// break/continue inside a range-int loop.
	sum := 0
	for i := range 10 {
		if i == 7 {
			break
		}
		if i%2 == 0 {
			continue
		}
		sum += i
	}
	println("sum", sum)
}
