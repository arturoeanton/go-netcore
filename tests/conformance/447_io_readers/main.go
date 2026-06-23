package main

import (
	"bytes"
	"fmt"
	"io"
	"strings"
)

// A user function consuming an io.Reader (a shim strings.Reader) through the
// interface, plus io.MultiReader / TeeReader / LimitReader composed of shim
// readers and drained by the io.Copy / io.ReadAll shims.
func readAll(r io.Reader) string {
	buf := make([]byte, 4)
	out := ""
	for {
		n, err := r.Read(buf)
		out += string(buf[:n])
		if err != nil {
			break
		}
	}
	return out
}

func main() {
	fmt.Println(readAll(strings.NewReader("hello world")))

	// MultiReader concatenates in order; drained by io.Copy and io.ReadAll.
	var dst bytes.Buffer
	n, _ := io.Copy(&dst, io.MultiReader(strings.NewReader("ab"), strings.NewReader("cd")))
	fmt.Println(n, dst.String())

	b, _ := io.ReadAll(io.MultiReader(strings.NewReader("x"), bytes.NewReader([]byte("yz"))))
	fmt.Println(string(b))

	// err == io.EOF identity holds for a shim reader.
	rr := strings.NewReader("q")
	tmp := make([]byte, 8)
	rr.Read(tmp)
	_, err := rr.Read(tmp)
	fmt.Println(err == io.EOF)

	// LimitReader and TeeReader.
	lr := io.LimitReader(strings.NewReader("0123456789"), 4)
	lb, _ := io.ReadAll(lr)
	fmt.Println(string(lb))

	var tee bytes.Buffer
	tr := io.TeeReader(strings.NewReader("tee!"), &tee)
	tb, _ := io.ReadAll(tr)
	fmt.Println(string(tb), tee.String())
}
