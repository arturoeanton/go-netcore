package main

import (
	"fmt"
	"net"
)

// net.ParseCIDR returns the original IP plus an *IPNet whose IP is masked to the network
// number and whose Mask is the prefix mask — both readable as fields (n.IP / n.Mask), with
// String() printing the canonical masked "network/prefix".
func main() {
	for _, c := range []string{
		"192.168.1.55/24", "10.20.30.40/16", "172.16.5.5/12", "1.2.3.4/32", "0.0.0.0/0",
		"10.0.0.0/8", "2001:db8::1/32", "fe80::1/64", "::1/128", "2001:db8:abcd:1234::/48",
		"255.255.255.255/30",
	} {
		ip, ipnet, err := net.ParseCIDR(c)
		fmt.Printf("%-22s ip=%v net=%v netIP=%v mask=%v str=%v err=%v\n",
			c, ip, ipnet, ipnet.IP, ipnet.Mask, ipnet.String(), err)
		fmt.Println("  contains-self:", ipnet.Contains(ipnet.IP), "contains-orig:", ipnet.Contains(ip))
	}
	_, _, e := net.ParseCIDR("not-a-cidr")
	fmt.Println(e)
	_, _, e2 := net.ParseCIDR("1.2.3.4/40")
	fmt.Println(e2)
}
