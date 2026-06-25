package main

import (
	"fmt"
	"net"
)

// net.IP / net.IPMask / net.HardwareAddr are named []byte with a String() method; fmt
// prints them via String() (for %v and %s), not as a raw byte slice.
func main() {
	fmt.Println(net.ParseIP("192.168.1.1"))
	fmt.Println(net.ParseIP("::1"))
	fmt.Println(net.ParseIP("2001:db8::1"))
	fmt.Printf("%v %s\n", net.ParseIP("10.0.0.1"), net.ParseIP("10.0.0.1"))
	fmt.Println(net.IPv4(127, 0, 0, 1))

	fmt.Println(net.CIDRMask(24, 32))
	fmt.Println(net.IPv4Mask(255, 255, 255, 0))

	mac, _ := net.ParseMAC("01:23:45:67:89:ab")
	fmt.Println(mac)
	fmt.Printf("%s\n", mac)

	// IP in a slice and through ParseCIDR
	ips := []net.IP{net.ParseIP("1.1.1.1"), net.ParseIP("8.8.8.8")}
	fmt.Println(ips)
	ip, _, _ := net.ParseCIDR("172.16.0.0/12")
	fmt.Println(ip, ip.To4())

	// Sprint / Sprintf
	fmt.Println(fmt.Sprint(net.ParseIP("9.9.9.9")))
	fmt.Println(fmt.Sprintf("addr=%s", net.ParseIP("203.0.113.1")))
}
