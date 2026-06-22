package main

import (
	"bufio"
	"bytes"
	"fmt"
	"hash/adler32"
	"html"
	"math"
	"net"
)

func main() {
	// hash/adler32.New (running digest)
	h := adler32.New()
	h.Write([]byte("Wikipedia"))
	fmt.Println("adler:", h.Sum32(), adler32.Checksum([]byte("Wikipedia")))

	// math.Pow10
	fmt.Println("pow10:", math.Pow10(0), math.Pow10(3), math.Pow10(6))

	// html.EscapeString / UnescapeString
	s := `<a href="x">'&'</a>`
	e := html.EscapeString(s)
	fmt.Println("esc:", e)
	fmt.Println("unesc:", html.UnescapeString(e) == s)

	// net IPv4zero family + To4 reassignment (the fasthttp ParseIPv4 pattern)
	var dst net.IP = make([]byte, net.IPv4len)
	copy(dst, net.IPv4zero)
	dst = dst.To4()
	fmt.Println("ipv4zero len:", len(dst), "bcast4:", net.IPv4bcast.To4()[0])

	// bufio.Reader.ReadByte + UnreadByte
	r := bufio.NewReader(bytes.NewReader([]byte("AB")))
	b1, _ := r.ReadByte()
	r.UnreadByte()
	b2, _ := r.ReadByte()
	b3, _ := r.ReadByte()
	fmt.Println("bufio:", string(b1), string(b2), string(b3))
}
