package main

import (
	"fmt"
	"strings"
)

func work() (err error) {
	defer func() {
		if r := recover(); r != nil {
			err = fmt.Errorf("recovered: %v", r)
		}
	}()
	defer fmt.Println("cleanup", 1, 2)
	defer fmt.Printf("[%s=%d]\n", "k", 7)
	defer fmt.Println(strings.Repeat("ab", 3))
	panic("kaboom")
}

func main() {
	fmt.Println("start")
	if err := work(); err != nil {
		fmt.Println("err:", err)
	}
	fmt.Println("done")
}
