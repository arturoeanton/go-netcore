package main

import "fmt"

func main() {
	buf := []byte("start:")
	buf = fmt.Appendf(buf, " %d+%d=%d", 2, 3, 5)
	buf = fmt.Append(buf, " tail", 42, true)
	buf = fmt.Appendln(buf, " end")
	fmt.Printf("%q\n", string(buf))
	fmt.Println("len:", len(buf))
}
