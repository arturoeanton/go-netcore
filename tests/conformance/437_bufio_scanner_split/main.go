package main

import (
	"bufio"
	"fmt"
	"strings"
)

// bufio.Scanner.Split selects the split function: ScanWords, ScanRunes, ScanBytes,
// or the default ScanLines.
func main() {
	w := bufio.NewScanner(strings.NewReader("  the quick  brown   fox  "))
	w.Split(bufio.ScanWords)
	n := 0
	for w.Scan() {
		n++
		fmt.Printf("[%s]", w.Text())
	}
	fmt.Println(" words:", n)

	r := bufio.NewScanner(strings.NewReader("aé世"))
	r.Split(bufio.ScanRunes)
	for r.Scan() {
		fmt.Printf("(%s)", r.Text())
	}
	fmt.Println()

	b := bufio.NewScanner(strings.NewReader("hi!"))
	b.Split(bufio.ScanBytes)
	for b.Scan() {
		fmt.Printf("%d ", b.Bytes()[0])
	}
	fmt.Println()

	l := bufio.NewScanner(strings.NewReader("L1\nL2\r\nL3"))
	l.Split(bufio.ScanLines)
	for l.Scan() {
		fmt.Printf("<%s>", l.Text())
	}
	fmt.Println()
}
