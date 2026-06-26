package main

import (
	"bufio"
	"errors"
	"fmt"
	"strings"
)

// bufio.Scanner enforces the Buffer(buf, max) token-size cap: a token that fills the
// buffer to its max (>= max bytes) fails Scan() and reports bufio.ErrTooLong via Err().
// Previously Buffer was a no-op and the scanner never errored.
func main() {
	for _, n := range []int{15, 16, 17, 32} {
		sc := bufio.NewScanner(strings.NewReader(strings.Repeat("x", n)))
		sc.Buffer(make([]byte, 4), 16)
		ok := sc.Scan()
		fmt.Printf("n=%d scan=%v tooLong=%v text=%d\n", n, ok, errors.Is(sc.Err(), bufio.ErrTooLong), len(sc.Text()))
	}

	// multiple tokens; the scan stops at the over-long one
	sc2 := bufio.NewScanner(strings.NewReader("ab\ncdefghijklmnopqrstuv\nxy"))
	sc2.Buffer(make([]byte, 4), 8)
	var got []string
	for sc2.Scan() {
		got = append(got, sc2.Text())
	}
	fmt.Printf("%q err=%v\n", got, sc2.Err())

	// ScanWords with an over-long word
	sc3 := bufio.NewScanner(strings.NewReader("ok " + strings.Repeat("z", 100)))
	sc3.Buffer(make([]byte, 4), 16)
	sc3.Split(bufio.ScanWords)
	r1 := sc3.Scan()
	t1 := sc3.Text()
	r2 := sc3.Scan()
	fmt.Printf("%v %q %v %v\n", r1, t1, r2, errors.Is(sc3.Err(), bufio.ErrTooLong))

	// default (no Buffer): normal-size lines scan fine with no error
	sc4 := bufio.NewScanner(strings.NewReader("alpha\nbeta\ngamma"))
	cnt := 0
	for sc4.Scan() {
		cnt++
	}
	fmt.Println(cnt, sc4.Err())
}
