package main

import (
	"bytes"
	"fmt"
	"strings"
)

// Replace with an empty old inserts new before each rune and at the end
// (Count(s,"")==runes+1); Trim/TrimLeft/TrimRight with an empty cutset trim nothing.
// Both must match Go (and not crash on .NET's empty-pattern APIs).
func main() {
	// strings.Replace / ReplaceAll with empty old
	fmt.Printf("%q\n", strings.Replace("abc", "", "-", -1))
	fmt.Printf("%q\n", strings.Replace("abcd", "", "-", 2))
	fmt.Printf("%q\n", strings.ReplaceAll("a世b", "", "|"))
	fmt.Printf("%q\n", strings.Replace("世界", "", "_", -1))
	fmt.Printf("%q\n", strings.Replace("🎉x", "", ".", -1))

	// strings.Replace normal + remove-all
	fmt.Println(strings.Replace("hello world", "o", "0", -1))
	fmt.Printf("%q\n", strings.Replace("aaa", "a", "", -1))
	fmt.Println(strings.ReplaceAll("a.b.c", ".", "/"))

	// strings.Trim/TrimLeft/TrimRight with empty cutset trim nothing
	fmt.Printf("%q %q %q\n", strings.Trim("  x  ", ""), strings.TrimLeft("aax", ""), strings.TrimRight("xaa", ""))
	fmt.Printf("%q\n", strings.Trim("xxhelloxx", "x"))

	// bytes.Replace / ReplaceAll with empty old
	fmt.Printf("%q\n", bytes.Replace([]byte("abc"), []byte(""), []byte("-"), -1))
	fmt.Printf("%q\n", bytes.ReplaceAll([]byte("a世b"), []byte(""), []byte("|")))
	fmt.Printf("%q\n", bytes.Replace([]byte("abcd"), []byte(""), []byte("-"), 2))

	// bytes.Trim with empty cutset
	fmt.Printf("%q %q\n", bytes.Trim([]byte("  x  "), ""), bytes.TrimLeft([]byte("aax"), ""))
	fmt.Printf("%q\n", bytes.Trim([]byte("##yo##"), "#"))
}
