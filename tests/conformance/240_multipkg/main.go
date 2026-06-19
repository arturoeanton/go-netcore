package main

import "github.com/arturoeanton/go-netcore/tests/conformance/240_multipkg/calc"

var label = "result"

func main() {
	println(label, calc.Add(2, 3))
	println(calc.Add(10, 20))
	println(calc.Calls())
	println(calc.Sum([]int{1, 2, 3, 4, 5}))

	var acc calc.Accumulator
	acc.Add(100)
	acc.Add(50)
	println(acc.Total())
	println(calc.Offset)
}
