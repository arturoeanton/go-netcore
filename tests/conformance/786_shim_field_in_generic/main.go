package main

import (
	"fmt"
	"net/url"
)

// Writing to an opaque-shim field (url.URL.Path) inside a GENERIC function. A pointer
// to a value-type shim IS that shim's handle, so *url.URL must resolve to the shim
// object (KObject) — not a pointer-to-object — even under the generic type-parameter
// substitution, or the field write fails to lower. This is httprc's
// ResourceBase[T].Sync doing res.Body = … on a *http.Response.
func setPath[T any](u *url.URL, p string) {
	u.Path = p
}

func main() {
	u, _ := url.Parse("http://example.com/original")
	setPath[int](u, "/updated")
	fmt.Println("path:", u.Path)
	fmt.Println("url:", u.String())
}
