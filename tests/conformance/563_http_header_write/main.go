package main

import (
	"bytes"
	"fmt"
	"net/http"
)

func main() {
	h := http.Header{}
	h.Set("Content-Type", "text/plain")
	h.Set("Authorization", "Bearer xyz")
	h.Add("X-Multi", "a")
	h.Add("X-Multi", "b")
	h.Add("Accept", "text/html")
	h.Add("X-Trim", "  padded \t")

	var buf bytes.Buffer
	if err := h.Write(&buf); err != nil {
		fmt.Println("err:", err)
	}
	fmt.Printf("written=%q\n", buf.String())

	// Empty header writes nothing.
	var empty http.Header
	var b2 bytes.Buffer
	empty.Write(&b2)
	fmt.Printf("empty=%q\n", b2.String())
}
