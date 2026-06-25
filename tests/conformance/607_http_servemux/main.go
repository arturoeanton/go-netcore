package main

import (
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
)

// net/http.ServeMux routes by pattern (subtree for "/"-suffixed, exact otherwise,
// longest match wins) and dispatches the handler with (w, r).
func main() {
	mux := http.NewServeMux()
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) { fmt.Fprint(w, "root") })
	mux.HandleFunc("/api/", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "api:%s", r.URL.Path)
	})
	mux.HandleFunc("/api/users", func(w http.ResponseWriter, r *http.Request) { fmt.Fprint(w, "users") })
	mux.HandleFunc("/hello", func(w http.ResponseWriter, r *http.Request) {
		name := r.URL.Query().Get("name")
		if name == "" {
			name = "world"
		}
		w.Header().Set("X-Custom", "yes")
		fmt.Fprintf(w, "Hello, %s!", name)
	})
	mux.HandleFunc("/echo", func(w http.ResponseWriter, r *http.Request) {
		b, _ := io.ReadAll(r.Body)
		w.WriteHeader(http.StatusCreated)
		w.Write(b)
	})
	mux.HandleFunc("/form", func(w http.ResponseWriter, r *http.Request) {
		r.ParseForm()
		fmt.Fprintf(w, "a=%s b=%s", r.FormValue("a"), r.FormValue("b"))
	})

	serve := func(method, target string, body io.Reader, ct string) (int, string, string) {
		req := httptest.NewRequest(method, target, body)
		if ct != "" {
			req.Header.Set("Content-Type", ct)
		}
		rec := httptest.NewRecorder()
		mux.ServeHTTP(rec, req)
		return rec.Code, rec.Header().Get("X-Custom"), rec.Body.String()
	}

	for _, p := range []string{"/", "/foo", "/api/", "/api/x", "/api/users", "/hello?name=Alice", "/hello"} {
		c, _, b := serve("GET", p, nil, "")
		fmt.Printf("%-18s %d %s\n", p, c, b)
	}

	c, x, b := serve("GET", "/hello?name=Bob", nil, "")
	fmt.Println(c, x, b)

	c, _, b = serve("POST", "/echo", strings.NewReader("payload"), "")
	fmt.Println(c, b)

	form := url.Values{"a": {"1"}, "b": {"two"}}
	c, _, b = serve("POST", "/form", strings.NewReader(form.Encode()), "application/x-www-form-urlencoded")
	fmt.Println(c, b)

	// http.Handler value via Handle
	mux2 := http.NewServeMux()
	mux2.Handle("/h", http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { fmt.Fprint(w, "iface") }))
	rec := httptest.NewRecorder()
	mux2.ServeHTTP(rec, httptest.NewRequest("GET", "/h", nil))
	fmt.Println(rec.Body.String())

	fmt.Println(http.StatusText(200), http.StatusText(404), http.StatusText(500))
}
