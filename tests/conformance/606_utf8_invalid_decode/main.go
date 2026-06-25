package main

import (
	"fmt"
	"unicode/utf8"
)

// utf8 decoding follows Go's byte-level rules: an invalid/overlong/surrogate/truncated
// sequence decodes to (RuneError, 1), and RuneCount counts each invalid byte once.
func main() {
	cases := [][]byte{
		{0xff},
		{0xc0, 0x80},
		{0xc1, 0xbf},
		{0xe0, 0x80, 0x80},
		{0xed, 0xa0, 0x80},
		{0xf0, 0x80, 0x80, 0x80},
		{0xf4, 0x90, 0x80, 0x80},
		{0xf5, 0x80, 0x80, 0x80},
		{0xc3},
		{0xe2, 0x82},
		{0x80},
		{0xe2, 0x82, 0xac},
		{0xf0, 0x9f, 0x98, 0x80},
		{0x41},
		{},
		{0xc3, 0xa9, 0xff},
	}
	for _, b := range cases {
		r, sz := utf8.DecodeRune(b)
		lr, lsz := utf8.DecodeLastRune(b)
		fmt.Printf("%x -> r=%d sz=%d | last=%d sz=%d | valid=%v count=%d\n",
			b, r, sz, lr, lsz, utf8.Valid(b), utf8.RuneCount(b))
	}

	// range over a string with embedded invalid bytes
	s := string([]byte{0x41, 0xff, 0x42, 0xe2, 0x82, 0xac})
	for i, r := range s {
		fmt.Printf("%d:%d ", i, r)
	}
	fmt.Println(len([]rune(s)), utf8.RuneCountInString(s))
}
