package main

import (
	"fmt"
	"strconv"
)

func main() {
	cases := []string{
		"hello", "a\tb\nc", "quote\"and\\back", "\x00\x01\x1f\x7f",
		"café", "no break", "emoji \U0001F600 here", "\a\b\f\v",
		"​́", "soft­hyphen", "bell\aend",
	}
	for _, c := range cases {
		fmt.Printf("Q=%s QA=%s\n", strconv.Quote(c), strconv.QuoteToASCII(c))
	}
	for _, r := range []rune{'A', '\t', 0x7f, 0xa0, 0xad, 0x1F600, '世', 0x0301} {
		fmt.Printf("QR=%s QRA=%s QRG=%s print=%v graphic=%v\n",
			strconv.QuoteRune(r), strconv.QuoteRuneToASCII(r), strconv.QuoteRuneToGraphic(r),
			strconv.IsPrint(r), strconv.IsGraphic(r))
	}
	// %q via fmt should match too.
	fmt.Printf("%q %q\n", "tab\tnbsp ", '\x1f')
}
