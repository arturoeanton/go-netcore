package main

import (
	"fmt"
	"net/http"
	"net/http/httptest"
)

func main() {
	req := httptest.NewRequest("GET", "/", nil)
	req.AddCookie(&http.Cookie{Name: "session", Value: "abc123"})
	req.AddCookie(&http.Cookie{Name: "theme", Value: "dark mode"}) // space -> quoted
	req.AddCookie(&http.Cookie{Name: "lang", Value: "en"})
	fmt.Printf("cookie header=%q\n", req.Header.Get("Cookie"))

	// Round-trips back through Cookies().
	for _, ck := range req.Cookies() {
		fmt.Printf("parsed %q=%q\n", ck.Name, ck.Value)
	}

	// Single cookie on a fresh request.
	r2 := httptest.NewRequest("GET", "/", nil)
	r2.AddCookie(&http.Cookie{Name: "only", Value: "x"})
	fmt.Printf("single=%q\n", r2.Header.Get("Cookie"))
}
