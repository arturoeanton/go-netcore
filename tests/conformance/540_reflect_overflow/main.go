package main

import (
	"fmt"
	"reflect"
)

func main() {
	fmt.Println("i8 200", reflect.ValueOf(int8(0)).OverflowInt(200))
	fmt.Println("i8 100", reflect.ValueOf(int8(0)).OverflowInt(100))
	fmt.Println("i16 40000", reflect.ValueOf(int16(0)).OverflowInt(40000))
	fmt.Println("i32 5e9", reflect.ValueOf(int32(0)).OverflowInt(5000000000))
	fmt.Println("i64 max", reflect.ValueOf(int64(0)).OverflowInt(1<<62))
	fmt.Println("u8 300", reflect.ValueOf(uint8(0)).OverflowUint(300))
	fmt.Println("u8 200", reflect.ValueOf(uint8(0)).OverflowUint(200))
	fmt.Println("f32 big", reflect.ValueOf(float32(0)).OverflowFloat(1e40))
	fmt.Println("f32 ok", reflect.ValueOf(float32(0)).OverflowFloat(1e30))
	fmt.Println("f64 big", reflect.ValueOf(float64(0)).OverflowFloat(1e40))
	fmt.Println("c64 big", reflect.ValueOf(complex64(0)).OverflowComplex(complex(1e40, 0)))
	fmt.Println("c64 ok", reflect.ValueOf(complex64(0)).OverflowComplex(complex(1, 2)))
	fmt.Println("c128 big", reflect.ValueOf(complex128(0)).OverflowComplex(complex(1e40, 1e40)))

	s := []int{1, 2, 3}
	reflect.ValueOf(s).Clear()
	fmt.Println("cleared slice", s)
	m := map[string]int{"a": 1, "b": 2}
	reflect.ValueOf(m).Clear()
	fmt.Println("cleared map len", len(m))

	g := []int{1, 2, 3}
	v := reflect.ValueOf(&g).Elem()
	v.Grow(10)
	fmt.Println("grown len", len(g), "cap>=13", cap(g) >= 13, "elems", g)

	base := []int{0, 1, 2, 3, 4, 5}
	sl := reflect.ValueOf(base).Slice3(1, 3, 5)
	fmt.Println("slice3 len", sl.Len(), "cap", sl.Cap(), "i0", sl.Index(0).Int(), "i1", sl.Index(1).Int())
}
