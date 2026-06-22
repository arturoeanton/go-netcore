package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
)

func main() {
	var buf bytes.Buffer
	tmp := make([]byte, binary.MaxVarintLen64)
	for _, v := range []uint64{0, 1, 127, 128, 300, 16384, 1 << 40} {
		n := binary.PutUvarint(tmp, v)
		buf.Write(tmp[:n])
	}
	r := bytes.NewReader(buf.Bytes())
	for i := 0; i < 7; i++ {
		x, err := binary.ReadUvarint(r)
		fmt.Println(x, err)
	}

	var sb bytes.Buffer
	for _, v := range []int64{0, -1, 1, -300, 300, -(1 << 40)} {
		n := binary.PutVarint(tmp, v)
		sb.Write(tmp[:n])
	}
	sr := bytes.NewReader(sb.Bytes())
	for i := 0; i < 6; i++ {
		x, err := binary.ReadVarint(sr)
		fmt.Println(x, err)
	}
}
