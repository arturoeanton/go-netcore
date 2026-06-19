package main

import "math"

func main() {
	println(int(math.Sqrt(144)))
	println(int(math.Pow(2, 10)))
	println(int(math.Floor(3.7)), int(math.Ceil(3.2)))
	println(int(math.Abs(-5.5) * 2))
	println(int(math.Max(3, 7)), int(math.Min(3, 7)))
	println(math.IsNaN(math.NaN()))
	println(math.IsInf(math.Inf(1), 1))
	println(int(math.Round(2.5)))
	println(int(math.Mod(17, 5)))
	println(int(math.Hypot(3, 4)))
	println(int(math.Trunc(9.99)))
	println(int(math.Log2(8)))
}
