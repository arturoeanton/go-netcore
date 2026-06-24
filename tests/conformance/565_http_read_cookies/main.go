package main

import (
	"errors"
	"fmt"
	"net/http"
	"net/http/httptest"
)

func main() {
	req := httptest.NewRequest("GET", "/", nil)
	req.Header.Set("Cookie", `session=abc; pref="dark mode"; bad name=skip; n=v2; blank=; =noname; valid=ok`)

	for _, n := range []string{"session", "pref", "n", "valid", "blank", "absent", ""} {
		ck, err := req.Cookie(n)
		if err != nil {
			fmt.Printf("Cookie(%q) -> err, isNoCookie=%v\n", n, errors.Is(err, http.ErrNoCookie))
			continue
		}
		fmt.Printf("Cookie(%q) -> %q=%q\n", n, ck.Name, ck.Value)
	}

	all := req.Cookies()
	fmt.Printf("Cookies count=%d\n", len(all))
	for _, c := range all {
		fmt.Printf("  %q=%q\n", c.Name, c.Value)
	}
}
