package main

import (
	"unicode"
	"unicode/utf8"
)

func main() {
	println(unicode.IsDigit('5'), unicode.IsLetter('A'), unicode.IsSpace(' '))
	println(unicode.IsUpper('G'), unicode.IsLower('g'))
	println(unicode.ToUpper('a'), unicode.ToLower('Z'))
	println(utf8.RuneCountInString("héllo"), utf8.RuneCountInString("日本語"))
	println(utf8.ValidString("ok"), utf8.RuneLen('A'), utf8.RuneLen('日'))
	println(string(rune(65)), string([]byte{104, 105}), string([]rune{97, 98, 99}))
}
