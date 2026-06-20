package main

import "fmt"

func classify(x any) (s string) {
	func() {
		switch v := x.(type) {
		case int:
			s = fmt.Sprintf("int:%d", v)
		case string:
			s = "str:" + v
		default:
			s = "other"
		}
	}()
	return
}

func main() {
	fmt.Println(classify(42), classify("hi"), classify(1.5))
}
