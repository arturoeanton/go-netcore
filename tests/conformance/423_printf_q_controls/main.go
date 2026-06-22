package main

import "fmt"

// %q escapes non-printable ASCII, including the control range 0x00-0x1F and DEL
// (0x7F), as \xNN, while leaving printable text and valid UTF-8 intact.
func main() {
	fmt.Printf("%q\n", "tab\there\nnewline\"quote\\back")
	fmt.Printf("%q\n", "ctrl \x00 \x01 \x1f del \x7f end")
	fmt.Printf("%q\n", "unicode 世界 ok")
	fmt.Printf("%q\n", []string{"a", "b\tc"})
	fmt.Printf("%q %q\n", 'A', '世')
}
