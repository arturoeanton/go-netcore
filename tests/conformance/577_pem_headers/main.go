package main

import (
	"encoding/pem"
	"fmt"
)

func main() {
	// Headers: Proc-Type is written first, the rest sorted, then a blank line.
	block := &pem.Block{
		Type:    "ENCRYPTED MESSAGE",
		Headers: map[string]string{"Version": "1", "Proc-Type": "4,ENCRYPTED", "DEK-Info": "AES-256-CBC,abc123"},
		Bytes:   []byte("this is a somewhat longer payload so the base64 spans several wrapped lines in the PEM body"),
	}
	enc := pem.EncodeToMemory(block)
	fmt.Printf("%s", enc)

	dec, rest := pem.Decode(enc)
	fmt.Printf("type=%q ver=%q proc=%q dek=%q bytes=%q rest=%d hdrs=%d\n",
		dec.Type, dec.Headers["Version"], dec.Headers["Proc-Type"], dec.Headers["DEK-Info"],
		dec.Bytes, len(rest), len(dec.Headers))

	// No headers: no blank line; decoded headers map is non-nil and empty.
	b2 := &pem.Block{Type: "CERTIFICATE", Bytes: []byte("certdata")}
	fmt.Printf("%s", pem.EncodeToMemory(b2))
	d2, _ := pem.Decode(pem.EncodeToMemory(b2))
	fmt.Printf("d2 type=%q nil=%v len=%d\n", d2.Type, d2.Headers == nil, len(d2.Headers))

	// A constructed block with nil headers reports nil.
	b3 := &pem.Block{Type: "X", Bytes: []byte("y")}
	fmt.Printf("b3 nil=%v len=%d get=%q\n", b3.Headers == nil, len(b3.Headers), b3.Headers["x"])

	// Trailing data after a block is returned as rest.
	multi := append(append([]byte{}, enc...), []byte("trailing\n")...)
	_, r := pem.Decode(multi)
	fmt.Printf("rest=%q\n", r)
}
