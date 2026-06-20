package main

import (
	"fmt"
	"strconv"
)

func parse(s string) (value interface{}, err error) {
	value, err = strconv.ParseInt(s, 0, 64)
	return
}

func main() {
	v, err := parse("42")
	fmt.Println(v, err)
	if n, ok := v.(int64); ok {
		fmt.Println("int64:", n+1)
	}
}
