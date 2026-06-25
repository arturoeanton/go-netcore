package main

import (
	"fmt"
	"strconv"
)

// strconv.Unquote accepts the empty single-quoted form '' (returning ""), a single
// rune, but rejects two or more characters; plus the usual quoted/raw/escape forms.
func main() {
	cases := []string{
		`''`, `'a'`, `'世'`, `'\n'`, `'\x41'`, `'\''`, `'ab'`, `'abc'`,
		`"hello"`, `"\t\n\r\\\""`, `"\x41\x42"`, `"\U0001F389"`, `"\101\102"`,
		"`raw\nstr`", `"café"`,
	}
	for _, q := range cases {
		u, err := strconv.Unquote(q)
		fmt.Printf("%q -> %q err=%v\n", q, u, err)
	}

	// error forms
	for _, q := range []string{`"unterminated`, `"\q"`, `bad`, `"\x"`, `"\u123"`, ``, `"`} {
		_, err := strconv.Unquote(q)
		fmt.Printf("%q err=%v\n", q, err != nil)
	}

	// QuotedPrefix
	p, _ := strconv.QuotedPrefix(`"abc"def`)
	fmt.Printf("%q\n", p)
	p2, _ := strconv.QuotedPrefix(`'x'rest`)
	fmt.Printf("%q\n", p2)
}
