package main

import (
	"bytes"
	"fmt"
	"net"
	"net/url"
	"regexp"
	"strings"
)

// fmt of a shim type that has a String() method must use it, not print the internal
// struct/handle: *url.Userinfo, *regexp.Regexp, *bytes.Buffer, net.Flags (and the
// already-working *url.URL, net.IP, time.Duration, *strings.Builder).
func main() {
	// url.Userinfo
	fmt.Println(url.UserPassword("bob", "secret"))
	fmt.Println(url.User("alice"))
	fmt.Printf("%v %s\n", url.UserPassword("u", "p"), url.UserPassword("u", "p"))

	// regexp.Regexp prints its pattern.
	re := regexp.MustCompile(`a+b*c?`)
	fmt.Println(re)
	fmt.Printf("%v %s\n", re, re)

	// bytes.Buffer prints its contents.
	var b bytes.Buffer
	b.WriteString("hello buffer")
	fmt.Println(&b)
	fmt.Printf("%v|%s\n", &b, &b)

	// net.Flags prints the flag names.
	fmt.Println(net.FlagUp | net.FlagBroadcast | net.FlagMulticast)
	fmt.Println(net.Flags(0))

	// Already-working Stringers, as a regression guard.
	var sb strings.Builder
	sb.WriteString("builder")
	fmt.Println(&sb)
	u, _ := url.Parse("https://x.com/p?q=1")
	fmt.Printf("%v %s\n", u, u)
	fmt.Println(net.ParseIP("10.0.0.1"))
}
