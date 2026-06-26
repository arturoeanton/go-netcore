package main

import (
	"fmt"
	"math/big"
	"net"
)

// Regression for two struct/parse paths that were broken:
//   - a composite-literal net.IPNet (IP + Mask set directly, no ParseCIDR) must
//     String()/Contains() correctly — String prints the IP as-is (not masked)
//     plus the prefix length, and a non-canonical mask prints as hex.
//   - big.Rat.SetString must accept decimal/exponent floats ("0.25", "1.5e3"),
//     not only "a/b" fractions and integers.
func main() {
	nets := []net.IPNet{
		{IP: net.IPv4(192, 168, 0, 0), Mask: net.CIDRMask(16, 32)},
		{IP: net.IPv4(10, 0, 0, 0), Mask: net.CIDRMask(24, 32)},
		{IP: net.IPv4(0, 0, 0, 0), Mask: net.CIDRMask(0, 32)},
		{IP: net.IPv4(255, 255, 255, 255), Mask: net.CIDRMask(32, 32)},
		{IP: net.IPv4(172, 16, 5, 4), Mask: net.IPv4Mask(255, 255, 0, 0)}, // unmasked IP
		{IP: net.ParseIP("2001:db8::"), Mask: net.CIDRMask(48, 128)},
	}
	for _, n := range nets {
		fmt.Println(n.String(),
			n.Contains(net.ParseIP("192.168.5.5")),
			n.Contains(net.ParseIP("10.0.0.99")))
	}
	// Non-canonical mask -> hex form.
	nc := net.IPNet{IP: net.IPv4(10, 0, 0, 0), Mask: net.IPv4Mask(255, 0, 255, 0)}
	fmt.Println(nc.String())
	// Mask field read-back.
	nn := net.IPNet{IP: net.IPv4(1, 2, 3, 4), Mask: net.CIDRMask(20, 32)}
	fmt.Println(nn.Mask, net.IP(nn.IP))

	// big.Rat.SetString across forms.
	for _, s := range []string{"0.25", "-3.14", "1.5e3", "2.5e-2", "100", "1/7", "-0.001", "0.", "5.", ".5", "1_000.5", "3e10", "0/5", "abc", "1/0"} {
		if r, ok := new(big.Rat).SetString(s); ok {
			fmt.Printf("%-8s -> %s (%s)\n", s, r.RatString(), r.FloatString(6))
		} else {
			fmt.Printf("%-8s -> !ok\n", s)
		}
	}
	fmt.Println(new(big.Rat).SetFloat64(0.1).FloatString(20))
	fmt.Println(big.NewRat(22, 7).FloatString(10))
}
