package main

import "fmt"

func try(f func() int) (out int, msg string) {
	defer func() {
		if r := recover(); r != nil {
			msg = fmt.Sprintf("%v", r)
		}
	}()
	out = f()
	return
}

func main() {
	// integer divide by zero is a recoverable runtime panic
	a, b := 10, 0
	_, m := try(func() int { return a / b })
	fmt.Println("div:", m)

	// modulo by zero too
	_, m = try(func() int { return a % b })
	fmt.Println("mod:", m)

	// unsigned divide by zero
	var u, z uint = 42, 0
	_, m = try(func() int { return int(u / z) })
	fmt.Println("udiv:", m)

	// non-zero divisor still computes normally
	ok, m := try(func() int { return 100 / 7 })
	fmt.Println("ok:", ok, "msg:", m)

	// a recovered panic lets execution continue
	fmt.Println("survived")
}
