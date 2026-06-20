package main

import "fmt"

type prop struct {
	writable bool
	name     string
}
type arr struct {
	lengthProp prop
	n          int
}
type slc struct{ arr }

func main() {
	o := &slc{}
	o.lengthProp.writable = true
	o.lengthProp.name = "len"
	o.n = 7
	o.n += 3
	fmt.Println(o.lengthProp.writable, o.lengthProp.name, o.n)
}
