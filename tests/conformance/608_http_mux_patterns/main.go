package main

import (
	"fmt"
	"net/http"
	"net/http/httptest"
)

// Go 1.22 ServeMux patterns: a leading method, {name} path wildcards (read via
// PathValue), {name...} match-all, method-specific precedence, and 405 when the path
// matches but the method does not.
func main() {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /items", func(w http.ResponseWriter, r *http.Request) { fmt.Fprint(w, "list") })
	mux.HandleFunc("POST /items", func(w http.ResponseWriter, r *http.Request) { fmt.Fprint(w, "create") })
	mux.HandleFunc("GET /items/{id}", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "item=%s", r.PathValue("id"))
	})
	mux.HandleFunc("GET /users/{uid}/posts/{pid}", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "u=%s p=%s", r.PathValue("uid"), r.PathValue("pid"))
	})
	mux.HandleFunc("GET /files/{path...}", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "path=%s", r.PathValue("path"))
	})
	// method-agnostic fallback subtree
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) { fmt.Fprint(w, "ok") })

	cases := []struct{ method, path string }{
		{"GET", "/items"},
		{"POST", "/items"},
		{"DELETE", "/items"},
		{"GET", "/items/42"},
		{"GET", "/users/alice/posts/7"},
		{"GET", "/files/a/b/c.txt"},
		{"GET", "/health"},
		{"PUT", "/health"},
		{"GET", "/missing"},
	}
	for _, c := range cases {
		rec := httptest.NewRecorder()
		mux.ServeHTTP(rec, httptest.NewRequest(c.method, c.path, nil))
		fmt.Printf("%-6s %-24s %d %q\n", c.method, c.path, rec.Code, rec.Body.String())
	}
}
