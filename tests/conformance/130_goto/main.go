package main

func main() {
	// Backward goto forming a loop.
	i := 0
loop:
	if i < 3 {
		println(i)
		i++
		goto loop
	}
	println("done")

	// Forward goto skipping a statement.
	if i == 3 {
		goto skip
	}
	println("not reached")
skip:
	println("after skip")

	// goto used to break out of nested loops.
	for a := 0; a < 3; a++ {
		for b := 0; b < 3; b++ {
			if a == 1 && b == 1 {
				goto out
			}
			println(a, b)
		}
	}
out:
	println("out")
}
