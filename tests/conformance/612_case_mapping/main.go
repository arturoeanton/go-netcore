package main

import (
	"bytes"
	"fmt"
	"strings"
)

// strings/bytes ToUpper/ToLower/ToTitle apply Go's per-rune simple case mapping over the
// whole Unicode range (not just ASCII), including the chars .NET's invariant mapping
// misses: the Turkish dotted/dotless I, long s, Kelvin/Angstrom, and the titlecase
// digraphs.
func main() {
	cases := []string{
		"Hello World", "ÀÉÎÕÜ àéîõü", "ÑÇ ñç", "ΑΒΓ αβγ ΩΨ ωψ", "АБВ абв",
		"ßẞ", "İıIi", "ﬀﬁﬂ", "K Å", "Mixed123!@#", "日本語", "ǅǄǆ", "Ⅳⅷ",
	}
	for _, s := range cases {
		fmt.Printf("up=%q lo=%q ti=%q\n", strings.ToUpper(s), strings.ToLower(s), strings.ToTitle(s))
	}

	// bytes variants apply the same Unicode mapping
	fmt.Printf("%s|%s\n", bytes.ToUpper([]byte("ñç hi")), bytes.ToLower([]byte("ÑÇ HI")))
	fmt.Printf("%s\n", bytes.ToLower([]byte("İ")))
	fmt.Printf("%s\n", bytes.ToTitle([]byte("ǅ word")))
	fmt.Printf("%s|%s\n", bytes.ToUpper([]byte("αβγ")), bytes.ToLower([]byte("ΩΨ")))
}
