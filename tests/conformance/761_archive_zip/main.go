package main

import (
	"archive/zip"
	"bytes"
	"crypto/sha256"
	"fmt"
	"io"
)

// archive/zip writer/reader round-trip. The compressed bytes are not reproducible (the
// compressor differs from Go's), but every entry name and its decompressed content round-trip
// byte-exact with go run.
func main() {
	var buf bytes.Buffer
	zw := zip.NewWriter(&buf)
	files := []struct{ name, body string }{
		{"readme.txt", "hello zip world"},
		{"dir/nested.txt", "nested content here"},
		{"empty.txt", ""},
		{"data/big.bin", string(bytes.Repeat([]byte("AB"), 500))},
		{"unicode.txt", "héllo 世界 🌍"},
	}
	for _, f := range files {
		w, err := zw.Create(f.name)
		if err != nil {
			fmt.Println("create err:", err)
			return
		}
		io.WriteString(w, f.body)
	}
	if err := zw.Close(); err != nil {
		fmt.Println("close err:", err)
		return
	}
	fmt.Println("zip nonempty:", buf.Len() > 0)

	zr, err := zip.NewReader(bytes.NewReader(buf.Bytes()), int64(buf.Len()))
	if err != nil {
		fmt.Println("reader err:", err)
		return
	}
	fmt.Println("num files:", len(zr.File))
	for _, f := range zr.File {
		rc, err := f.Open()
		if err != nil {
			fmt.Println("open err:", err)
			continue
		}
		b, _ := io.ReadAll(rc)
		rc.Close()
		fmt.Printf("%-16s size=%-4d sha=%x\n", f.Name, len(b), sha256.Sum256(b))
	}
}
