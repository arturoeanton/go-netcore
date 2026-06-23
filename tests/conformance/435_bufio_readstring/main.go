package main

import (
	"bufio"
	"fmt"
	"strings"
)

// bufio.Reader.ReadString / ReadBytes read up to and including a delimiter, returning
// io.EOF with the trailing data when the delimiter is absent at end of input.
func main() {
	r := bufio.NewReader(strings.NewReader("alpha\nbeta\ngamma"))
	for {
		line, err := r.ReadString('\n')
		fmt.Printf("%q\n", line)
		if err != nil {
			break
		}
	}

	r2 := bufio.NewReader(strings.NewReader("a,b,c,"))
	for {
		b, err := r2.ReadBytes(',')
		fmt.Printf("[%s] %d\n", b, len(b))
		if err != nil {
			break
		}
	}

	// Scanner still works alongside.
	sc := bufio.NewScanner(strings.NewReader("x\ny\nz"))
	n := 0
	for sc.Scan() {
		n++
	}
	fmt.Println("lines:", n)
}
