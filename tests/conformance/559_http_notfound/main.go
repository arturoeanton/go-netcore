package main

import (
	"fmt"
	"net/http"
	"net/http/httptest"
)

func main() {
	rec := httptest.NewRecorder()
	http.NotFound(rec, httptest.NewRequest("GET", "/missing", nil))
	fmt.Printf("notfound code=%d body=%q ctype=%q nosniff=%q\n",
		rec.Code, rec.Body.String(), rec.Header().Get("Content-Type"), rec.Header().Get("X-Content-Type-Options"))

	r2 := httptest.NewRecorder()
	http.Error(r2, "something broke", http.StatusInternalServerError)
	fmt.Printf("error code=%d body=%q ctype=%q\n", r2.Code, r2.Body.String(), r2.Header().Get("Content-Type"))
}
