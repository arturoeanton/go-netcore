package main

import (
	"fmt"
	"net/url"
)

// Reading a field through a *shimType (a pointer to an opaque stdlib shim) uses the
// same getter as the value form: the shim pointer is the same handle. url.Parse
// already returns a *url.URL, so passing it on and reading fields through the
// pointer exercises the pointer-to-shim field read path.
func host(u *url.URL) string { return u.Host }
func scheme(u *url.URL) string { return u.Scheme }

func main() {
	u, err := url.Parse("https://example.com:8443/path?q=1")
	if err != nil {
		fmt.Println("err:", err)
		return
	}
	fmt.Println(scheme(u), host(u), u.Path)
}
