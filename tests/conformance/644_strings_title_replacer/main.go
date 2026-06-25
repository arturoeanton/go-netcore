package main

import (
	"fmt"
	"strings"
)

// strings.Title uses Go's isSeparator (ASCII letters/digits/'_' are never separators, so
// "foo_bar" -> "Foo_bar" and "WORLD" stays uppercase) — NOT .NET ToTitleCase. And
// strings.Replacer matches/advances on UTF-8 *bytes* with Go's priority + prevMatchEmpty
// rule, so an empty old key inserts its replacement between every byte (NewReplacer("","X",
// "a","b").Replace("aa") == "XbXbX", and "" over a multibyte rune splits between its bytes).
func main() {
	for _, s := range []string{
		"hello world", "HELLO", "foo_bar baz", "a1b2", "x'y z", "über café",
		"ǳenan", "  lead", "tab\tsep", "123 abc", "_under", "mixedCASE here", "",
	} {
		fmt.Printf("%q\n", strings.Title(s))
	}

	fmt.Println(strings.NewReplacer("", "X", "a", "b").Replace("aa"))
	fmt.Println(strings.NewReplacer("a", "b", "", "X").Replace("aa"))
	fmt.Println(strings.NewReplacer("", "-").Replace("abc"))
	fmt.Println(strings.NewReplacer("a", "1", "ab", "2", "abc", "3").Replace("abcabxa"))
	fmt.Println(strings.NewReplacer("abc", "3", "ab", "2", "a", "1").Replace("abcab"))
	fmt.Println(strings.NewReplacer("<", "&lt;", ">", "&gt;").Replace("a<b>c"))
	fmt.Println(strings.NewReplacer("", "X").Replace(""))
	fmt.Println(strings.NewReplacer("ab", "X").Replace("ababab"))
	fmt.Printf("%q\n", strings.NewReplacer("", "_").Replace("héllo"))
	fmt.Printf("%q\n", strings.NewReplacer("世", "W").Replace("a世b世"))
	fmt.Println(strings.NewReplacer("ø", "o", "æ", "ae").Replace("smørrebrød"))
}
