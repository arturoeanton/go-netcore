package main

func main() {
	// labeled continue: skip to the next outer iteration.
outer:
	for i := 0; i < 3; i++ {
		for j := 0; j < 3; j++ {
			if j == 1 {
				continue outer
			}
			println(i, j)
		}
	}

	// labeled break: leave both loops at once.
search:
	for i := 0; i < 3; i++ {
		for j := 0; j < 3; j++ {
			if i == 1 && j == 1 {
				println("found", i, j)
				break search
			}
			println("scan", i, j)
		}
	}
	println("after search")

	// labeled break over a range loop.
top:
	for i := 0; i < 4; i++ {
		for _, v := range []int{10, 20, 30} {
			if v == 20 {
				break top
			}
			println("range", i, v)
		}
	}
	println("end")
}
