package main

import "fmt"

type Str interface{ s() string }
type myStr string

func (m myStr) s() string { return string(m) }

type Obj struct{ n int }

// A type switch with an interface case (Str) and a concrete pointer case (*Obj):
// an *Obj must NOT route into the Str arm (it does not implement Str).
func classify(v interface{}) string {
	switch x := v.(type) {
	case Str:
		return "str:" + x.s()
	case *Obj:
		return fmt.Sprintf("obj:%d", x.n)
	case int:
		return fmt.Sprintf("int:%d", x)
	default:
		return "other"
	}
}

func main() {
	fmt.Println(classify(myStr("hi")))
	fmt.Println(classify(&Obj{7}))
	fmt.Println(classify(42))
	fmt.Println(classify(1.5))
}
