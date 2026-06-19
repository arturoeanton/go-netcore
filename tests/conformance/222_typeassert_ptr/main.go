package main

type A struct{ x int }
type B struct{ y int }

func describe(i interface{}) int {
	switch v := i.(type) {
	case *A:
		return v.x
	case *B:
		return v.y
	case int:
		return v
	default:
		return -1
	}
}

func main() {
	println(describe(&A{5}))
	println(describe(&B{7}))
	println(describe(42))
	println(describe("x"))

	var i interface{} = &A{9}
	if a, ok := i.(*A); ok {
		println("A", a.x)
	}
	if _, ok := i.(*B); ok {
		println("B")
	} else {
		println("not B")
	}
}
