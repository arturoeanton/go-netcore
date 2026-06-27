package main

import (
	"bytes"
	"fmt"
	"mime/multipart"
	"sort"
	"strings"
)

// mime/multipart writer/reader round-trip. The boundary is random (30 bytes → 60 hex chars,
// like Go's multipart.randomBoundary), so its content isn't reproducible, but its length/format
// and the full field/file round-trip are deterministic and byte-exact with go run.
func main() {
	var b bytes.Buffer
	w := multipart.NewWriter(&b)

	fmt.Println("boundary len:", len(w.Boundary()))
	fmt.Println("boundary hex:", isHex(w.Boundary()))
	fmt.Println("content-type prefix:", strings.HasPrefix(w.FormDataContentType(), "multipart/form-data; boundary="))

	w.WriteField("name", "alice")
	w.WriteField("city", "NYC")
	fw, _ := w.CreateFormFile("doc", "test.txt")
	fw.Write([]byte("file contents here"))
	w.Close()

	r := multipart.NewReader(&b, w.Boundary())
	type part struct{ name, filename, body string }
	var parts []part
	for {
		p, err := r.NextPart()
		if err != nil {
			break
		}
		buf := new(bytes.Buffer)
		buf.ReadFrom(p)
		parts = append(parts, part{p.FormName(), p.FileName(), buf.String()})
	}
	sort.Slice(parts, func(i, j int) bool { return parts[i].name < parts[j].name })
	for _, p := range parts {
		fmt.Printf("name=%q file=%q body=%q\n", p.name, p.filename, p.body)
	}

	// SetBoundary accepts a custom value and rejects an over-long one.
	var b2 bytes.Buffer
	w2 := multipart.NewWriter(&b2)
	fmt.Println("set ok:", w2.SetBoundary("myCustomBoundary123") == nil)
	fmt.Println("set boundary:", w2.Boundary())
	fmt.Println("set toolong err:", w2.SetBoundary(strings.Repeat("x", 80)) != nil)
}

func isHex(s string) bool {
	for _, c := range s {
		if !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) {
			return false
		}
	}
	return true
}
