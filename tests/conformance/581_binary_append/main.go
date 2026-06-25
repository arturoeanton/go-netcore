package main

import (
	"encoding/binary"
	"fmt"
)

func main() {
	// AppendUint16/32/64 (Go 1.19) in both byte orders.
	var b []byte
	b = binary.BigEndian.AppendUint16(b, 0x0102)
	b = binary.BigEndian.AppendUint32(b, 0xcafebabe)
	b = binary.BigEndian.AppendUint64(b, 0x1122334455667788)
	fmt.Printf("be=%x\n", b)

	var l []byte
	l = binary.LittleEndian.AppendUint16(l, 0x0102)
	l = binary.LittleEndian.AppendUint32(l, 0xcafebabe)
	l = binary.LittleEndian.AppendUint64(l, 0x1122334455667788)
	fmt.Printf("le=%x\n", l)

	// Appending onto an existing slice grows it.
	pre := []byte{0xaa, 0xbb}
	pre = binary.BigEndian.AppendUint32(pre, 0xdeadbeef)
	fmt.Printf("pre=%x len=%d\n", pre, len(pre))

	// Round-trip with Uint32.
	rt := binary.BigEndian.AppendUint32(nil, 0x12345678)
	fmt.Printf("rt=%08x\n", binary.BigEndian.Uint32(rt))
}
