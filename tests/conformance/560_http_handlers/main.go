package main

import (
	"fmt"
	"net/http"
	"net/http/httptest"
)

func main() {
	// NotFoundHandler(): a Handler whose ServeHTTP replies 404.
	rec := httptest.NewRecorder()
	var h http.Handler = http.NotFoundHandler()
	h.ServeHTTP(rec, httptest.NewRequest("GET", "/missing", nil))
	fmt.Printf("nf code=%d body=%q ctype=%q nosniff=%q\n",
		rec.Code, rec.Body.String(), rec.Header().Get("Content-Type"), rec.Header().Get("X-Content-Type-Options"))

	// RedirectHandler(url, code): a Handler that redirects, with the html body for GET.
	for _, c := range []int{http.StatusMovedPermanently, http.StatusFound, http.StatusTemporaryRedirect, http.StatusPermanentRedirect} {
		r2 := httptest.NewRecorder()
		http.RedirectHandler("/new/place", c).ServeHTTP(r2, httptest.NewRequest("GET", "/old", nil))
		fmt.Printf("rh code=%d loc=%q ctype=%q body=%q\n",
			r2.Code, r2.Header().Get("Location"), r2.Header().Get("Content-Type"), r2.Body.String())
	}

	// A HEAD request gets the headers but no body.
	r3 := httptest.NewRecorder()
	http.RedirectHandler("/x", 301).ServeHTTP(r3, httptest.NewRequest("HEAD", "/old", nil))
	fmt.Printf("head code=%d loc=%q body=%q\n", r3.Code, r3.Header().Get("Location"), r3.Body.String())
}
