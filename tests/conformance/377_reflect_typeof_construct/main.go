package main

import (
	"fmt"
	"reflect"
)

// reflect type construction: MapOf/SliceOf/PtrTo/ArrayOf synthesize composite type
// descriptors from element/key reflect.Types.
func main() {
	st := reflect.TypeOf("")
	it := reflect.TypeOf(0)
	mt := reflect.MapOf(st, it)
	fmt.Println(mt.Kind(), mt.String(), mt.Key().Kind(), mt.Elem().Kind())
	sl := reflect.SliceOf(it)
	fmt.Println(sl.Kind(), sl.String(), sl.Elem().Kind())
	pt := reflect.PtrTo(st)
	fmt.Println(pt.Kind(), pt.String(), pt.Elem().Kind())
	at := reflect.ArrayOf(3, it)
	fmt.Println(at.Kind(), at.String(), at.Len(), at.Elem().Kind())
	// nested
	mm := reflect.MapOf(st, reflect.SliceOf(it))
	fmt.Println(mm.String(), mm.Elem().String())
}
