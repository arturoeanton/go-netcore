package main

import "bytes"

func main() {
	a := []byte("hello world")
	println(bytes.Contains(a, []byte("wor")))
	println(bytes.HasPrefix(a, []byte("hell")), bytes.HasSuffix(a, []byte("rld")))
	println(bytes.Index(a, []byte("o")), bytes.Count(a, []byte("o")))
	println(bytes.Equal([]byte("ab"), []byte("ab")), bytes.Equal([]byte("ab"), []byte("ac")))
	println(string(bytes.ToUpper([]byte("Go"))), string(bytes.ToLower([]byte("GoLang"))))
	println(string(bytes.TrimSpace([]byte("  hi  "))))
	println(string(bytes.Repeat([]byte("ab"), 3)))
	parts := bytes.Split([]byte("a,b,c"), []byte(","))
	println(len(parts), string(parts[0]), string(parts[2]))
	println(string(bytes.Join(parts, []byte("-"))))
	println(bytes.Compare([]byte("a"), []byte("b")))
}
