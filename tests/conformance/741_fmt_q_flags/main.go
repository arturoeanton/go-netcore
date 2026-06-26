package main

import "fmt"

// %q honors the '#' and '+' flags like Go: '#' prefers a raw-string literal (backquotes)
// when the value can be back-quoted, else falls back to a double-quoted string; '+' escapes
// all non-ASCII (QuoteToASCII / QuoteRuneToASCII). The sharp/backquote test wins over plus.
func main() {
	// '#': back-quote when possible, else double-quote.
	fmt.Printf("%#q %#q %#q %#q %#q\n", "raw", "has\ttab", "has\nnl", "has`tick", "has\x00null")
	// '+': ASCII-escape non-ASCII.
	fmt.Printf("%+q %+q %+q\n", "héllo", "世界", "emoji🎉")
	// Runes.
	fmt.Printf("%q %+q %q %+q\n", '世', '世', 'A', 'A')
	// Slices of strings.
	fmt.Printf("%q\n%+q\n%#q\n", []string{"a", "héllo", "raw"}, []string{"a", "héllo"}, []string{"a", "plain", "x"})
	// Byte slices.
	fmt.Printf("%q %+q %#q\n", []byte("café"), []byte("café"), []byte("raw"))
	// Map (keys + values).
	fmt.Printf("%q\n%+q\n", map[string]string{"k": "héllo", "raw": "v"}, map[string]string{"é": "ü"})
	// Plain %q unaffected.
	fmt.Printf("%q %q\n", "héllo", "tab\there")
}
