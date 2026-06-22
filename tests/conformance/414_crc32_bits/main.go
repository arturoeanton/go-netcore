package main

import (
	"fmt"
	"hash/crc32"
	"math/bits"
)

func main() {
	data := []byte("The quick brown fox jumps over the lazy dog")
	fmt.Println(crc32.ChecksumIEEE(data))

	c := crc32.Update(0, crc32.IEEETable, data[:10])
	c = crc32.Update(c, crc32.IEEETable, data[10:])
	fmt.Println(c)

	h := crc32.NewIEEE()
	h.Write(data[:20])
	h.Write(data[20:])
	fmt.Println(h.Sum32())
	h.Reset()
	h.Write(data)
	fmt.Println(h.Sum32())

	fmt.Println(bits.Reverse8(0x1), bits.Reverse8(0xa5))
	fmt.Println(bits.Reverse16(0x1), bits.Reverse16(0x1234))
}
