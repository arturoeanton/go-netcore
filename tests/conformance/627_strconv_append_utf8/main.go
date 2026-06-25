package main

import (
	"fmt"
	"strconv"
)

// strconv.Append* builds onto a []byte; for the quoting variants whose output keeps
// non-ASCII runes (AppendQuote, AppendQuoteRune, AppendQuoteToGraphic) the runes must
// be UTF-8 encoded into the slice, not truncated to a single byte.
func main() {
	fmt.Printf("%q\n", string(strconv.AppendQuoteRune(nil, '世')))
	fmt.Printf("%q\n", string(strconv.AppendQuoteRune(nil, 'a')))
	fmt.Printf("%q\n", string(strconv.AppendQuoteRune(nil, '🎉')))
	fmt.Printf("%q\n", string(strconv.AppendQuote(nil, "世界 hello")))
	fmt.Printf("%q\n", string(strconv.AppendQuoteToASCII(nil, "café")))

	// appending onto an existing (possibly multibyte) buffer
	buf := []byte("τ = ")
	buf = strconv.AppendFloat(buf, 6.28318, 'f', 3, 64)
	buf = append(buf, ' ')
	buf = strconv.AppendInt(buf, -42, 10)
	buf = append(buf, ' ')
	buf = strconv.AppendQuote(buf, "naïve 世")
	fmt.Println(string(buf))

	// the pure-ASCII appenders are unchanged
	fmt.Println(string(strconv.AppendInt(nil, 255, 16)))
	fmt.Println(string(strconv.AppendUint(nil, 1000, 2)))
	fmt.Println(string(strconv.AppendBool(nil, true)))
	fmt.Println(string(strconv.AppendQuoteRuneToASCII(nil, '世')))

	// round-trip: quote then unquote a multibyte string built via Append
	q := string(strconv.AppendQuote(nil, "Ωμέγα"))
	u, _ := strconv.Unquote(q)
	fmt.Println(q, u, u == "Ωμέγα")
}
