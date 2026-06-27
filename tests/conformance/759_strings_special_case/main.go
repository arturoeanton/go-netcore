package main

import (
	"fmt"
	"strings"
	"unicode"
)

// strings.ToUpperSpecial / ToLowerSpecial / ToTitleSpecial apply locale-aware casing through a
// unicode.SpecialCase ([]CaseRange). unicode is compiled, so the SpecialCase is a real slice of
// CaseRange structs; the casing follows unicode.to() (case index + delta, with the default
// mapping as fallback) and is byte-exact with go run.
func main() {
	fmt.Println(strings.ToUpperSpecial(unicode.TurkishCase, "diyarbakir"))
	fmt.Println(strings.ToLowerSpecial(unicode.TurkishCase, "İSTANBUL"))
	fmt.Println(strings.ToTitleSpecial(unicode.TurkishCase, "istanbul ankara"))
	fmt.Println(strings.ToUpperSpecial(unicode.TurkishCase, "İstanbul ığdır"))
	fmt.Println(strings.ToLowerSpecial(unicode.TurkishCase, "DİYARBAKIR"))
	fmt.Println(strings.ToUpperSpecial(unicode.AzeriCase, "ışıq"))

	// A custom SpecialCase: 'a' maps to itself (delta 0); other runes fall back to default.
	custom := unicode.SpecialCase{
		unicode.CaseRange{Lo: 'a', Hi: 'a', Delta: [unicode.MaxCase]rune{0, 0, 0}},
	}
	fmt.Println(strings.ToUpperSpecial(custom, "abc"))
	fmt.Println(strings.ToLowerSpecial(custom, "ABC"))

	// Non-special runes fall through to the default upper/lower/title mapping (digraph title).
	fmt.Println(strings.ToTitleSpecial(unicode.TurkishCase, "ǳürich"))
	fmt.Println(strings.ToUpperSpecial(unicode.TurkishCase, "héllo wörld 123"))
}
