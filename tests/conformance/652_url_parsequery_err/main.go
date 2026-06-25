package main

import (
	"fmt"
	"net/url"
)

// url.ParseQuery records the first error (a malformed %XX, or a ';' separator which Go 1.17+
// rejects) but still parses the remaining valid pairs into the map. Query() ignores the error.
func main() {
	for _, q := range []string{
		"a=1&a=2&b", "x=%zz&y=ok", "a;b=1", "valid=1&bad=%2",
		"=noKey&k=v", "a=%41%42&c=d", "key=hello+world", "p=%E4%B8%96", "",
	} {
		m, err := url.ParseQuery(q)
		fmt.Printf("%q -> err=%v vals=%v\n", q, err, m)
	}

	// (*URL).Query() discards the error but keeps valid pairs.
	u, _ := url.Parse("http://x/p?a=1&bad=%zz&b=2")
	qq := u.Query()
	fmt.Println(qq.Get("a"), qq.Get("b"), qq.Has("bad"))
}
