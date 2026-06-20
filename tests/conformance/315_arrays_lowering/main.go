package main

import "fmt"

type Pt struct{ X, Y int }

func two() (int, int) { return 7, 9 }

func main() {
	// fixed-size arrays (backed by slice)
	var a [4]int
	a[0], a[1] = 10, 20
	a[3] = 40
	fmt.Println(a, len(a), a[0]+a[3])
	b := [3]string{"x", "y", "z"}
	for i := range b {
		fmt.Print(b[i], " ")
	}
	fmt.Println()
	// int8/int16
	var i8 int8 = -5
	var i16 int16 = 300
	fmt.Println(i8, i16, i8+1, i16*2)
	// range over int/rune
	sum := 0
	for i := range 5 {
		sum += i
	}
	fmt.Println("sum:", sum)
	cnt := 0
	for range rune(10) {
		cnt++
	}
	fmt.Println("cnt:", cnt)
	// multi-assign to struct field + slice element
	var p Pt
	p.X, p.Y = two()
	fmt.Println(p)
	pts := []Pt{{1, 2}, {3, 4}}
	pts[1].X = 99
	fmt.Println(pts)
	arr := []int{0, 0}
	arr[0], arr[1] = two()
	fmt.Println(arr)
}
