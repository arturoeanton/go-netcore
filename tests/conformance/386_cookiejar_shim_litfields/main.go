// An opaque-shim composite literal must apply its field initializers (http.Cookie{...},
// previously only the zero value survived), and a net/http/cookiejar.Jar must store and
// return cookies with RFC 6265 domain/path matching.
package main

import (
	"fmt"
	"net/http"
	"net/http/cookiejar"
	"net/url"
)

func main() {
	c := &http.Cookie{Name: "session", Value: "xyz", Path: "/app", MaxAge: 120}
	fmt.Printf("cookie %s=%s path=%s maxage=%d\n", c.Name, c.Value, c.Path, c.MaxAge)

	jar, _ := cookiejar.New(nil)
	u, _ := url.Parse("https://example.com/app/page")
	jar.SetCookies(u, []*http.Cookie{
		{Name: "a", Value: "1", Path: "/"},
		{Name: "b", Value: "2", Path: "/app"},
	})
	got := jar.Cookies(u)
	fmt.Println("matched:", len(got))

	u2, _ := url.Parse("https://example.com/other")
	fmt.Println("other:", len(jar.Cookies(u2)))
}
