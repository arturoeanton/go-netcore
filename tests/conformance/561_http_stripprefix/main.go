package main

import (
	"fmt"
	"net/http"
	"net/http/httptest"
)

func main() {
	inner := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "served path=%s", r.URL.Path)
	})
	h := http.StripPrefix("/api/v1", inner)
	for _, p := range []string{"/api/v1/users", "/api/v1", "/api/v1/", "/other", "/api/v1xyz"} {
		rec := httptest.NewRecorder()
		h.ServeHTTP(rec, httptest.NewRequest("GET", p, nil))
		fmt.Printf("req=%-14s code=%d body=%q\n", p, rec.Code, rec.Body.String())
	}
	// Empty prefix returns the handler unchanged.
	h2 := http.StripPrefix("", inner)
	rec := httptest.NewRecorder()
	h2.ServeHTTP(rec, httptest.NewRequest("GET", "/untouched", nil))
	fmt.Printf("empty code=%d body=%q\n", rec.Code, rec.Body.String())
}
