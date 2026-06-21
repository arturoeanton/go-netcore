package main

import (
	"fmt"
	"reflect"
	"unsafe"
)

// subsliceOffset mirrors the go-toml idiom: the byte offset of a sub-slice within
// its parent backing array, computed from the difference of the Data fields.
func subsliceOffset(parent, sub []byte) int {
	pd := (*reflect.SliceHeader)(unsafe.Pointer(&parent)).Data
	sd := (*reflect.SliceHeader)(unsafe.Pointer(&sub)).Data
	return int(sd - pd)
}

func main() {
	data := []byte("hello, world")
	for _, j := range []int{0, 1, 5, 7, 12} {
		sub := data[j:]
		fmt.Println("slice off", j, "=", subsliceOffset(data, sub))
	}

	mid := data[3:9]
	fmt.Println("nested", subsliceOffset(data, mid[2:4]))

	hdr := (*reflect.SliceHeader)(unsafe.Pointer(&data))
	fmt.Println("len", hdr.Len, "cap", hdr.Cap)

	s := "abcdefghij"
	sh := (*reflect.StringHeader)(unsafe.Pointer(&s))
	fmt.Println("string len", sh.Len)
}
