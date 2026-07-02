package main

func main() {
	var a any = 5
	var b any = 5
	println("int5:", a == b)
	var c any = 5000000
	var d any = 5000000
	println("big:", c == d)
	var s1 any = "xy"
	var s2 any = "x" + "y"
	println("str:", s1 == s2)
	var e any = 5
	var f any = "5"
	println("mixed:", e == f)
	type P struct{ X int }
	var g any = P{1}
	var h any = P{1}
	println("struct:", g == h)
	var n1, n2 any
	println("nils:", n1 == n2)
	var bt1 any = true
	var bt2 any = true
	println("bools:", bt1 == bt2)

	// switch with an interface tag routes through the same equality
	var tag any = 7
	switch tag {
	case any(6):
		println("switch: six")
	case any(7):
		println("switch: seven")
	default:
		println("switch: none")
	}

	// != and comparisons inside conditions
	var x any = 10
	var y any = 10
	if x != y {
		println("neq: differ")
	} else {
		println("neq: same")
	}

	// typed-nil pointers of the same type compare equal through interfaces
	type T struct{ V int }
	var tp1 *T
	var tp2 *T
	var i1 any = tp1
	var i2 any = tp2
	println("typednil-pair:", i1 == i2)
	println("typednil-vs-nil:", i1 == nil)

	// errors: sentinel comparison through the error interface (same instance)
	e1 := mkerr("boom")
	e2 := e1
	println("err-same:", e1 == e2)
	println("err-diff:", e1 == mkerr("boom"))

	// strings built at runtime still compare by content
	p1 := "ab"
	var v1 any = p1 + "c"
	var v2 any = "a" + "bc"
	println("str-built:", v1 == v2)

	// float dynamic types
	var f1 any = 2.5
	var f2 any = 2.5
	println("floats:", f1 == f2)

	// interface vs CONCRETE operand (the err == syscall.EAGAIN shape): the concrete
	// side must box; equality is by dynamic type + value
	var ci any = 42
	println("concrete-eq:", ci == 42)
	println("concrete-ne:", ci == 43)
	var cs any = "go"
	println("concrete-str:", cs == "go")
	// concrete side in a switch over an interface tag
	switch ci {
	case 41:
		println("cswitch: 41")
	case 42:
		println("cswitch: 42")
	default:
		println("cswitch: none")
	}
}

type myErr struct{ s string }

func (e *myErr) Error() string { return e.s }

func mkerr(s string) error { return &myErr{s} }
