package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
)

type Header struct {
	Magic   uint32
	Version uint16
	Flags   uint16
}

type Record struct {
	H     Header
	ID    uint64
	Code  int16
	Kind  uint8
	Sign  int8
	Ratio float32
	Tags  [4]uint16
}

func main() {
	r := Record{
		H:     Header{0xdeadbeef, 0x0001, 0xABCD},
		ID:    0x1122334455667788,
		Code:  -1000,
		Kind:  200,
		Sign:  -7,
		Ratio: 2.5,
		Tags:  [4]uint16{10, 20, 30, 40},
	}
	for _, bo := range []struct {
		name  string
		order binary.ByteOrder
	}{{"BE", binary.BigEndian}, {"LE", binary.LittleEndian}} {
		var buf bytes.Buffer
		if err := binary.Write(&buf, bo.order, r); err != nil {
			panic(err)
		}
		var got Record
		if err := binary.Read(bytes.NewReader(buf.Bytes()), bo.order, &got); err != nil {
			panic(err)
		}
		fmt.Printf("%s bytes=%x size=%d roundtrip=%v\n", bo.name, buf.Bytes(), binary.Size(r), r == got)
	}

	// Standalone fixed-width scalars.
	var u16 uint16
	var i32 int32
	var u64 uint64
	binary.Read(bytes.NewReader([]byte{0x12, 0x34}), binary.BigEndian, &u16)
	binary.Read(bytes.NewReader([]byte{0xff, 0xff, 0xff, 0x9c}), binary.BigEndian, &i32)
	binary.Read(bytes.NewReader([]byte{1, 2, 3, 4, 5, 6, 7, 8}), binary.LittleEndian, &u64)
	fmt.Printf("u16=%#x i32=%d u64=%#x\n", u16, i32, u64)

	// Write a slice of structs.
	var sb bytes.Buffer
	binary.Write(&sb, binary.BigEndian, []Header{{1, 2, 3}, {4, 5, 6}})
	fmt.Printf("slice=%x\n", sb.Bytes())
}
