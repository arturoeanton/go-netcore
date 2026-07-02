package main

import (
	"fmt"
	"net/url"
)

// A comma-ok (tuple) assignment whose left target is an opaque-shim field setter
// (url.URL is a shim; Host/Path/Scheme have field setters). goclr must route the
// first result to the setter extern, and discard the blank.
func main() {
	var u url.URL
	var host any = "example.com"
	var missing any = 42

	u.Host, _ = host.(string)      // asserts to "example.com"
	u.Scheme, _ = missing.(string) // fails -> zero value ""
	u.Path, _ = any("/p").(string) // asserts to "/p"

	fmt.Println("host:", u.Host)
	fmt.Println("scheme:", u.Scheme == "")
	fmt.Println("path:", u.Path)
	fmt.Println("url:", u.String())
}
