package main

import (
	"fmt"
	"net"
	"net/url"
)

// fmt prints a *url.URL and a net.Addr (net.IPNet/TCPAddr/UDPAddr) via their String()
// method, not as a raw struct/byte dump.
func main() {
	u, _ := url.Parse("https://user@example.com:8080/path?q=1#frag")
	fmt.Println(u)
	fmt.Printf("%v %s\n", u, u)

	base, _ := url.Parse("https://example.com/a/b/c")
	ref, _ := url.Parse("../x")
	fmt.Println(base.ResolveReference(ref))

	_, ipnet, _ := net.ParseCIDR("10.0.0.0/24")
	fmt.Println(ipnet)
	fmt.Printf("%v\n", ipnet)

	urls := []*url.URL{u, base}
	fmt.Println(urls[0], urls[1])
	fmt.Println(u.String() == fmt.Sprint(u))
}
