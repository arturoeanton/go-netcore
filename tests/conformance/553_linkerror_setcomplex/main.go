package main

import (
	"errors"
	"fmt"
	"os"
	"reflect"
)

func main() {
	base := errors.New("file exists")
	le := &os.LinkError{Op: "symlink", Old: "/a", New: "/b", Err: base}
	fmt.Println("error:", le.Error())
	fmt.Printf("op=%q old=%q new=%q\n", le.Op, le.Old, le.New)
	fmt.Println("is base:", errors.Is(le, base))
	fmt.Println("unwrap:", errors.Unwrap(le).Error())

	// reflect.Value.SetComplex on an addressable complex.
	c := complex128(0)
	v := reflect.ValueOf(&c).Elem()
	v.SetComplex(complex(3, -4))
	fmt.Println("complex:", c, real(c), imag(c))
	fmt.Printf("reflect: %v\n", v.Complex())
}
