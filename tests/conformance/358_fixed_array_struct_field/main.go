package main

import "fmt"

type scanner struct {
	b     []byte
	bytes [16]byte
}

func makeScanner(s string) scanner {
	sc := scanner{}
	sc.b = sc.bytes[:copy(sc.bytes[:], s)]
	return sc
}

func main() {
	sc := makeScanner("en-US")
	fmt.Println(string(sc.b), len(sc.b))
	sc.b[0] = 'X'
	fmt.Println(string(sc.b))
}
