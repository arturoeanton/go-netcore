package main

import (
	"fmt"
	"strconv"
)

// strconv.Unquote treats a \x escape as a raw byte (and \u/\U/non-ASCII runes as UTF-8),
// so `"\xc3\xa9"` decodes to the two bytes of "é", not the runes U+00C3 U+00A9. Round-trips
// with Quote, including invalid-UTF-8 byte sequences.
func main() {
	for _, s := range []string{
		`"\xc3\xa9"`, `"é"`, `"\xff\xfe"`, `"caf\xc3\xa9"`, `"\x41\x42\x43"`,
		`"\101\102"`, `"世界"`, `"\U0001F600"`, `"mix\x20é\101"`,
		`"plain ascii"`, "`raw\tstring`", `"tab\there"`, `'A'`, `'世'`, `"\x00"`,
	} {
		u, err := strconv.Unquote(s)
		fmt.Printf("%s -> %q (len=%d) err=%v\n", s, u, len(u), err)
	}
	for _, orig := range []string{"héllo", "tab\tnl\n", "\xff invalid", "世界🎉", ""} {
		q := strconv.Quote(orig)
		u, _ := strconv.Unquote(q)
		fmt.Println(u == orig, q)
	}
	// Errors.
	for _, bad := range []string{`"unterminated`, `"\x"`, `"\xZZ"`, `'ab'`, `"\999"`} {
		_, err := strconv.Unquote(bad)
		fmt.Println(err != nil)
	}
}
