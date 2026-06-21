// net UDP: ResolveUDPAddr/ListenUDP/DialUDP and a UDPConn round-trip on the loopback,
// plus a &net.UDPAddr{...} literal and the promoted LocalAddr method.
package main

import (
	"fmt"
	"net"
)

func main() {
	la, _ := net.ResolveUDPAddr("udp", "127.0.0.1:0")
	srv, err := net.ListenUDP("udp", la)
	if err != nil {
		fmt.Println("listen:", err)
		return
	}
	defer srv.Close()
	addr := srv.LocalAddr().(*net.UDPAddr)

	cl, err := net.DialUDP("udp", nil, addr)
	if err != nil {
		fmt.Println("dial:", err)
		return
	}
	defer cl.Close()

	cl.Write([]byte("ping"))
	buf := make([]byte, 32)
	n, from, _ := srv.ReadFromUDP(buf)
	fmt.Printf("got %q\n", string(buf[:n]))
	srv.WriteToUDP([]byte("pong"), from)
	n2, _ := cl.Read(buf)
	fmt.Printf("reply %q\n", string(buf[:n2]))

	a := &net.UDPAddr{IP: net.ParseIP("10.0.0.1"), Port: 53}
	fmt.Println("addr:", a.String(), a.Port)
}
