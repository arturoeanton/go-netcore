package main

import "fmt"

type myErr struct{ code int }

func (e *myErr) Error() string {
	return fmt.Sprintf("err %d", e.code)
}

// mayFail returns a typed-nil *myErr when !fail — the classic Go gotcha: the returned
// error interface is NON-nil because it carries the *myErr dynamic type.
func mayFail(fail bool) error {
	var e *myErr
	if fail {
		e = &myErr{code: 7}
	}
	return e
}

func main() {
	var p *int
	var i any = p
	fmt.Println("typed nil *int in any == nil:", i == nil) // false

	var j any
	fmt.Println("bare any == nil:", j == nil) // true

	err := mayFail(false)
	fmt.Println("typed-nil error == nil:", err == nil) // false — the classic gotcha

	err2 := mayFail(true)
	fmt.Println("real error == nil:", err2 == nil, "msg:", err2.Error()) // false, "err 7"

	// A nil interface stays nil; an explicit non-nil also behaves.
	var k error
	fmt.Println("nil error == nil:", k == nil) // true
}
