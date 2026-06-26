package main

import (
	"fmt"
	"net/url"
)

// net/url edges: a nil *Userinfo (a URL with no userinfo) returns "" from its methods
// instead of panicking; a network-path reference (//authority/path, e.g. a protocol-
// relative URL) parses its authority as the host; and Hostname() strips the brackets of
// an IPv6-literal host.
func main() {
	for _, s := range []string{
		"https://user:pass@example.com:8080/path?q=1#f",
		"http://example.com/no/userinfo",
		"//cdn.example.com/lib.js",
		"http://[2001:db8::1]:443/x",
		"http://[::1]:8080/",
	} {
		u, err := url.Parse(s)
		if err != nil {
			fmt.Println("ERR", err)
			continue
		}
		fmt.Printf("scheme=%q host=%q path=%q user=%q hostname=%q port=%q str=%s\n",
			u.Scheme, u.Host, u.Path, u.User.String(), u.Hostname(), u.Port(), u.String())
	}
	// constructed Userinfo
	fmt.Println(url.User("alice").String(), url.UserPassword("u", "p").String())
}
