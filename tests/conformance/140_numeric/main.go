package main

func main() {
	var x float64 = 3.5
	var y float64 = 2.0
	z := x * y
	println(int(z))
	println(int(x / y * 100))
	pi := 3.14159
	println(int(pi * 1000))

	var a uint64 = 100
	var b uint64 = 7
	println(a / b)
	println(a % b)

	var big uint32 = 4000000000
	println(big)
	println(big / 2)

	if x > y {
		println("x>y")
	}
	if a < b {
		println("a<b")
	} else {
		println("a>=b")
	}

	n := 42
	f := float64(n) / 4.0
	println(int(f * 10))
}
