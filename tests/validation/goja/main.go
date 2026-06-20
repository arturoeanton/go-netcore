package main

import (
	"fmt"
	"github.com/dop251/goja"
)

func main() {
	vm := goja.New()
	v, err := vm.RunString(`1+2`)
	if err != nil {
		fmt.Println("err:", err)
		return
	}
	fmt.Println("result:", v.Export())
}
