package main

import (
	"bytes"
	"compress/flate"
	"errors"
	"fmt"
	"io"
)

func main() {
	// Named error types.
	var ci flate.CorruptInputError = 42
	fmt.Println(ci.Error())
	var ie flate.InternalError = "bad state"
	fmt.Println(ie.Error())

	// Struct error types (deprecated) with wrapped error.
	re := &flate.ReadError{Offset: 7, Err: errors.New("eof")}
	fmt.Println(re.Error(), "off=", re.Offset)
	we := &flate.WriteError{Offset: 13, Err: errors.New("disk full")}
	fmt.Println(we.Error())

	// Writer.Reset: compress two payloads with one writer, reset between.
	var b1, b2 bytes.Buffer
	w, _ := flate.NewWriter(&b1, 6)
	w.Write([]byte("hello hello hello"))
	w.Close()
	w.Reset(&b2)
	w.Write([]byte("world world world"))
	w.Close()
	// Round-trip the second to prove Reset produced valid deflate.
	r := flate.NewReader(&b2)
	out, _ := io.ReadAll(r)
	fmt.Printf("reset roundtrip=%q\n", string(out))
}
