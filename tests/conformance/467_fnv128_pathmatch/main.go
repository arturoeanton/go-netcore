package main

import (
	"errors"
	"fmt"
	"hash/fnv"
	"path"
)

func main() {
	for _, s := range []string{"", "a", "hello", "The quick brown fox"} {
		h := fnv.New128()
		h.Write([]byte(s))
		fmt.Printf("128 %q %x\n", s, h.Sum(nil))
		ha := fnv.New128a()
		ha.Write([]byte(s))
		fmt.Printf("128a %q %x\n", s, ha.Sum(nil))
	}
	fmt.Println("size:", fnv.New128().Size())

	cases := [][2]string{
		{"abc", "abc"}, {"a*c", "abc"}, {"a?c", "abc"}, {"a[bc]c", "abc"},
		{"a[^x]c", "abc"}, {"*/c", "a/c"}, {"*", "a/b"}, {"a*", "abc"},
		{"a\\*c", "a*c"}, {"a\\*c", "abc"}, {"[a-c]", "b"}, {"[!a]", "x"},
		{"h[a-z]llo", "hello"}, {"*foo*", "xfooy"},
	}
	for _, c := range cases {
		m, err := path.Match(c[0], c[1])
		fmt.Printf("Match(%q,%q)=%v err=%v\n", c[0], c[1], m, err)
	}

	_, e := path.Match("[", "a")
	fmt.Println("bad:", e, errors.Is(e, path.ErrBadPattern))
}
