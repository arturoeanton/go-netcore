package main

import (
	"encoding/hex"
	"errors"
	"fmt"
	"io"
	"strings"
)

func main() {
	dst := []byte("PRE:")
	dst = hex.AppendEncode(dst, []byte("hi"))
	fmt.Printf("%s\n", dst)
	d2, err := hex.AppendDecode([]byte("X:"), []byte("6869"))
	fmt.Printf("%s err=%v\n", d2, err)

	_, e1 := hex.DecodeString("xyz")
	fmt.Println("xyz:", e1)
	_, e2 := hex.DecodeString("abc")
	fmt.Println("abc:", e2, errors.Is(e2, hex.ErrLength))
	_, e3 := hex.DecodeString("6g")
	fmt.Println("6g:", e3)

	dec := hex.NewDecoder(strings.NewReader("68656c6c6f"))
	out, _ := io.ReadAll(dec)
	fmt.Printf("decoded=%q\n", out)

	// hex.Dump width handling across partial/full lines.
	for _, n := range []int{1, 7, 8, 13, 16, 17} {
		b := make([]byte, n)
		for i := range b {
			b[i] = byte('A' + i)
		}
		fmt.Print(hex.Dump(b))
	}
}
