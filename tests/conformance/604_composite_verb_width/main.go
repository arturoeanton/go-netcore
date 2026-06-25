package main

import "fmt"

// A width/precision flag on a numeric verb that recurses into a slice/map/struct is
// applied to each element, like Go (%03d of []int{5,42} -> "[005 042]"), not the whole
// composite.
type P struct{ X, Y int }

func main() {
	fmt.Printf("%03d\n", []int{5, 42})
	fmt.Printf("%5d\n", []int{1, 2})
	fmt.Printf("%03d\n", map[int]int{1: 5, 2: 42})
	fmt.Printf("%03d\n", P{5, 42})
	fmt.Printf("%03d\n", [][]int{{5}, {42}})
	fmt.Printf("%+03d\n", []int{1, -2})
	fmt.Printf("%6.2f\n", []float64{3.1, 2.5})
	fmt.Printf("%-4d|\n", []int{1, 22})
	fmt.Printf("%8.3e\n", []float64{1234.5})
	fmt.Printf("%04b\n", []int{5, 10})
	fmt.Printf("%05d\n", &P{1, 2})

	// no width: unchanged
	fmt.Printf("%d\n", []int{5, 42})
	fmt.Printf("%v\n", []int{1, 2})

	// scalar still pads the whole value
	fmt.Printf("%03d %6.2f\n", 7, 3.1)

	// nested struct + slice mix
	type Row struct{ Vals []int }
	fmt.Printf("%03d\n", Row{[]int{1, 2, 3}})
}
