package main

import (
	"fmt"
	"strconv"
)

func main() {
	vals := []float64{0, 1, 1.5, -2.25, 100000, 1234567, 0.0001, 0.00001,
		1e20, 1e21, 1e-7, 3.141592653589793, 2.0 / 3.0, 1234567890123456789,
		0.1, 123456.789, 1e100, 1000000, 999999.9, -0.5, 42.0}
	for _, v := range vals {
		fmt.Printf("%v|%g|", v, v)
		fmt.Println(v, strconv.FormatFloat(v, 'g', -1, 64))
	}
	fmt.Println(0.1 + 0.2)
	fmt.Println([]float64{1.5, 2.5, 1e10})
	fmt.Printf("%.2f %.3e\n", 3.14159, 1234.5)
}
