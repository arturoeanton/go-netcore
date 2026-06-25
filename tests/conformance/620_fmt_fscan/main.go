package main

import (
	"bytes"
	"errors"
	"fmt"
	"io"
	"strings"
)

// fmt.Fscan / Fscanf / Fscanln read from an io.Reader, consuming only the bytes they
// parse (so the idiomatic "scan one value per loop iteration" works) and returning
// io.EOF — not "unexpected EOF" — when the input runs out at a value boundary.
func main() {
	// basic Fscan / Fscanf / Fscanln across reader types
	var a, b int
	n, err := fmt.Fscan(strings.NewReader("42 17"), &a, &b)
	fmt.Println(n, err, a, b)

	var name string
	fmt.Fscanf(strings.NewReader("player:bob"), "player:%s", &name)
	fmt.Println(name)

	var f float64
	fmt.Fscanf(bytes.NewBufferString("x=3.14"), "x=%f", &f)
	fmt.Println(f)

	var w1, w2 string
	cnt, _ := fmt.Fscanln(strings.NewReader("hello world\nignored"), &w1, &w2)
	fmt.Println(cnt, w1, w2)

	// scan one value per loop iteration — the reader must NOT be fully drained
	r := strings.NewReader("1 2 3 4 5")
	sum := 0
	for {
		var x int
		if _, e := fmt.Fscan(r, &x); e != nil {
			break
		}
		sum += x
	}
	fmt.Println("sum", sum)

	// the same with a bytes.Buffer and mixed types
	buf := bytes.NewBufferString("alice 30 bob 25 carol 35")
	total := 0
	for {
		var nm string
		var age int
		if _, e := fmt.Fscan(buf, &nm, &age); e != nil {
			break
		}
		total += age
	}
	fmt.Println("ages", total)

	// underflow returns the real io.EOF (identity + message)
	var p, q int
	m, e := fmt.Fscan(strings.NewReader("9"), &p, &q)
	fmt.Println(m, e == io.EOF, errors.Is(e, io.EOF), fmt.Sprint(e))

	// Sscan/Sscanf underflow is io.EOF too
	_, se := fmt.Sscan("1 2", &p, &q, new(int))
	fmt.Printf("%v\n", se)
	_, fe := fmt.Sscanf("1", "%d %d", &p, &q)
	fmt.Printf("%v\n", fe)
}
