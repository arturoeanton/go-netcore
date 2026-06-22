package main

import (
	"bytes"
	"encoding/hex"
	"fmt"
	"io"
)

// seqReader is a user io.Reader: io.ReadFull must drive its Read through the bridge.
type seqReader struct{ n byte }

func (r *seqReader) Read(p []byte) (int, error) {
	for i := range p {
		p[i] = r.n
		r.n++
	}
	return len(p), nil
}

func main() {
	// bytes.EqualFold
	fmt.Println("equalfold:", bytes.EqualFold([]byte("GoCLR"), []byte("goclr")), bytes.EqualFold([]byte("a"), []byte("b")))

	// hex.Encode / hex.Decode (in-place forms)
	src := []byte{0xde, 0xad, 0xbe, 0xef}
	dst := make([]byte, hex.EncodedLen(len(src)))
	n := hex.Encode(dst, src)
	fmt.Println("hex.Encode:", string(dst[:n]))

	back := make([]byte, hex.DecodedLen(len(dst)))
	m, err := hex.Decode(back, dst)
	fmt.Printf("hex.Decode: %x n=%d err=%v\n", back[:m], m, err)

	// io.ReadFull driving a user io.Reader through the bridge.
	buf := make([]byte, 6)
	r := &seqReader{n: 10}
	k, err := io.ReadFull(r, buf)
	fmt.Println("ReadFull:", buf[:k], "err:", err)
}
