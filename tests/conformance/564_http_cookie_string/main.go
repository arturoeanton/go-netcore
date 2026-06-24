package main

import (
	"fmt"
	"net/http"
	"net/http/httptest"
)

func main() {
	cs := []*http.Cookie{
		{Name: "id", Value: "abc123"},
		{Name: "sess", Value: "needs quote", Path: "/app", Domain: ".example.com", MaxAge: 3600, HttpOnly: true, Secure: true},
		{Name: "comma", Value: "a,b"},
		{Name: "del", Value: "x", MaxAge: -1},
		{Name: "lax", Value: "v", SameSite: http.SameSiteLaxMode},
		{Name: "strict", Value: "v", SameSite: http.SameSiteStrictMode},
		{Name: "none", Value: "v", Secure: true, SameSite: http.SameSiteNoneMode},
		{Name: "def", Value: "v", SameSite: http.SameSiteDefaultMode},
		{Name: "bad name", Value: "v"},
	}
	for _, c := range cs {
		fmt.Printf("%q\n", c.String())
	}

	// SetCookie writes the Set-Cookie header on the recorder.
	rec := httptest.NewRecorder()
	http.SetCookie(rec, &http.Cookie{Name: "tok", Value: "v1", Path: "/", SameSite: http.SameSiteStrictMode})
	fmt.Printf("set-cookie=%q\n", rec.Header().Get("Set-Cookie"))
}
