package main

import (
	"encoding/ascii85"
	"fmt"
)

func main() {
	inputs := []string{
		"", "M", "Ma", "Man", "sure.",
		"Hello, ascii85! \xff\xfe\x00\x01",
		"\x00\x00\x00\x00",       // z shortcut
		"ab\x00\x00\x00\x00cd",   // z in the middle
		"\x00\x00\x00",           // partial zero group (no z)
	}
	for _, in := range inputs {
		src := []byte(in)
		dst := make([]byte, ascii85.MaxEncodedLen(len(src)))
		n := ascii85.Encode(dst, src)
		out := make([]byte, len(src)+8)
		nd, ns, err := ascii85.Decode(out, dst[:n], true)
		fmt.Printf("%q -> %q  rt=%q ndst=%d nsrc=%d err=%v\n", in, dst[:n], out[:nd], nd, ns, err)
	}
	// corrupt input reports the offending byte
	bad := []byte("12345\x01")
	o := make([]byte, 16)
	_, _, err := ascii85.Decode(o, bad, true)
	fmt.Println("corrupt:", err)
	// whitespace is skipped on decode
	o2 := make([]byte, 16)
	nd, _, _ := ascii85.Decode(o2, []byte("87cU RD_*#"), true)
	fmt.Printf("ws-decoded=%q\n", o2[:nd])
}
