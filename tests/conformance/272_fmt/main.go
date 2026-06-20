package main

import "fmt"

type Point struct{ X, Y int }

func main() {
	fmt.Println("hello", "world", 42)
	fmt.Printf("%s is %d years old\n", "Ada", 36)
	fmt.Printf("%v %+v\n", Point{1, 2}, Point{1, 2})
	fmt.Printf("%d %x %X %b %o\n", 255, 255, 255, 5, 8)
	fmt.Printf("%t %q\n", true, "quoted")
	fmt.Println(fmt.Sprintf("[%d-%s]", 7, "x"))
	fmt.Println([]int{1, 2, 3})
	err := fmt.Errorf("failed with code %d", 500)
	fmt.Println(err.Error())
	fmt.Printf("%v\n", &Point{3, 4})
	fmt.Print("a", "b", 1, 2, "\n")
}
