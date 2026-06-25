package main

import (
	"fmt"
	"net/url"
)

// Building a *url.URL from a composite literal (&url.URL{...}) must allocate a real
// backing value for its field setters — previously the zero value was nil and the
// first field-set nil-panicked. Covers Scheme/Host/Path/RawQuery/Fragment/User/Opaque
// and mutating the fields of a parsed URL.
func main() {
	u := &url.URL{
		Scheme:   "https",
		Host:     "example.com:8443",
		Path:     "/api/v1/users",
		RawQuery: "limit=10&sort=name",
		Fragment: "section",
	}
	fmt.Println(u.String())

	fmt.Println((&url.URL{Scheme: "ftp", User: url.UserPassword("admin", "secret"), Host: "files.example.com", Path: "/data"}).String())
	fmt.Println((&url.URL{Scheme: "mailto", Opaque: "user@example.com"}).String())
	fmt.Println((&url.URL{Path: "/just/a/path"}).String())
	fmt.Println((&url.URL{Scheme: "https", Host: "x.com"}).String())
	fmt.Println((&url.URL{Scheme: "https", User: url.User("bob"), Host: "h.com", Path: "/"}).String())

	// mutate a parsed URL's fields and re-stringify
	p, _ := url.Parse("http://old.com/path?a=1")
	p.Scheme = "https"
	p.Host = "new.com:443"
	p.Path = "/new/path"
	p.RawQuery = url.Values{"x": {"1"}, "y": {"2"}}.Encode()
	p.Fragment = "top"
	fmt.Println(p.String())

	// build query off a constructed base
	base := &url.URL{Scheme: "https", Host: "api.example.com", Path: "/search"}
	q := base.Query()
	q.Set("term", "hello world")
	q.Add("page", "1")
	base.RawQuery = q.Encode()
	fmt.Println(base.String())

	// ResolveReference from a constructed base
	b2 := &url.URL{Scheme: "https", Host: "site.com", Path: "/dir/page"}
	ref, _ := url.Parse("../other?q=1")
	fmt.Println(b2.ResolveReference(ref).String())

	// round-trips
	orig := "https://example.com:8080/a/b?c=d&e=f#g"
	rp, _ := url.Parse(orig)
	fmt.Println(rp.String() == orig)
}
