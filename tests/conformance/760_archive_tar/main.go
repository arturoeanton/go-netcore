package main

import (
	"archive/tar"
	"bytes"
	"crypto/sha256"
	"fmt"
	"io"
	"time"
)

// archive/tar — a USTAR writer/reader. With explicit header fields the produced archive is
// byte-identical to go run (so the sha256 matches), and the write→read round-trip recovers
// every header field and body byte-exact.
func main() {
	var buf bytes.Buffer
	tw := tar.NewWriter(&buf)
	mt := time.Date(2024, 6, 1, 12, 0, 0, 0, time.UTC)
	files := []struct {
		name, body string
		mode       int64
	}{
		{"readme.txt", "hello world", 0644},
		{"data/payload.bin", "abcdefghij0123456789ABCDEFGHIJ", 0600},
		{"empty", "", 0666},
		{"padded.txt", string(bytes.Repeat([]byte("x"), 700)), 0644},
	}
	for _, f := range files {
		hdr := &tar.Header{
			Name: f.name, Mode: f.mode, Size: int64(len(f.body)), ModTime: mt,
			Uid: 1000, Gid: 1000, Uname: "user", Gname: "grp", Typeflag: tar.TypeReg,
		}
		if err := tw.WriteHeader(hdr); err != nil {
			fmt.Println("hdr err:", err)
			return
		}
		io.WriteString(tw, f.body)
	}
	tw.Close()

	fmt.Printf("archive len=%d sha256=%x\n", buf.Len(), sha256.Sum256(buf.Bytes()))

	tr := tar.NewReader(bytes.NewReader(buf.Bytes()))
	for {
		h, err := tr.Next()
		if err == io.EOF {
			break
		}
		if err != nil {
			fmt.Println("next err:", err)
			break
		}
		body, _ := io.ReadAll(tr)
		fmt.Printf("%-18s size=%-4d mode=%o uid=%d uname=%s mtime=%s tf=%c sha=%x\n",
			h.Name, h.Size, h.Mode, h.Uid, h.Uname,
			h.ModTime.UTC().Format("2006-01-02T15:04:05"), h.Typeflag,
			sha256.Sum256(body))
	}
}
