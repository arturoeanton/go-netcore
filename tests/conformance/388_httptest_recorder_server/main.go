// net/http/httptest: an in-memory ResponseRecorder unit-tests a handler, and a live
// NewServer (a real loopback listener on a background thread) serves an http.HandlerFunc
// to an in-process client.
package main

import (
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
)

func handler(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path == "/created" {
		w.WriteHeader(http.StatusCreated)
	}
	fmt.Fprintf(w, "path=%s", r.URL.Path)
}

func main() {
	rec := httptest.NewRecorder()
	handler(rec, httptest.NewRequest("GET", "/created", nil))
	fmt.Println("recorder:", rec.Code, rec.Body.String())

	srv := httptest.NewServer(http.HandlerFunc(handler))
	defer srv.Close()
	resp, err := http.Get(srv.URL + "/hello")
	if err != nil {
		fmt.Println("get:", err)
		return
	}
	b, _ := io.ReadAll(resp.Body)
	resp.Body.Close()
	fmt.Println("server:", resp.StatusCode, string(b))
}
