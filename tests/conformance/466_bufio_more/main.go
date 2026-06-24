package main

import (
	"bufio"
	"bytes"
	"fmt"
	"strings"
)

func main() {
	r := bufio.NewReader(strings.NewReader("héllo wörld\nsecond line\nlast"))
	fmt.Println("size:", r.Size())

	ru, sz, _ := r.ReadRune()
	fmt.Printf("rune=%c size=%d\n", ru, sz)
	ru2, sz2, _ := r.ReadRune()
	fmt.Printf("rune=%c size=%d\n", ru2, sz2)
	r.UnreadRune()
	ru3, _, _ := r.ReadRune()
	fmt.Printf("after unread=%c\n", ru3)

	err := r.UnreadRune()
	r.ReadByte()
	e2 := r.UnreadRune()
	fmt.Println("unread1==nil:", err == nil, "unread2:", e2)

	line, pre, _ := r.ReadLine()
	fmt.Printf("line=%q prefix=%v\n", string(line), pre)
	sl, _ := r.ReadSlice('\n')
	fmt.Printf("slice=%q\n", string(sl))

	var sb bytes.Buffer
	n, _ := r.WriteTo(&sb)
	fmt.Printf("writeto n=%d rest=%q\n", n, sb.String())

	var out bytes.Buffer
	w := bufio.NewWriter(&out)
	fmt.Println("wsize:", w.Size())
	w.WriteRune('€')
	w.WriteString("xy")
	w.Flush()
	fmt.Printf("written=%q\n", out.String())

	var out2 bytes.Buffer
	w2 := bufio.NewWriter(&out2)
	nn, _ := w2.ReadFrom(strings.NewReader("from-reader"))
	w2.Flush()
	fmt.Printf("readfrom n=%d %q\n", nn, out2.String())

	fmt.Println(bufio.ErrTooLong, bufio.ErrFinalToken, bufio.ErrInvalidUnreadRune, bufio.ErrInvalidUnreadByte)
}
