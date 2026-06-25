package main

import (
	"fmt"
	"net/url"
)

// url.Parse decodes the path into URL.Path (percent-escapes resolved) and keeps the raw form
// in RawPath only when it is a non-default encoding; EscapedPath()/String()/RequestURI()
// reproduce the original encoding. A malformed %XX yields a url.Error reading
// `parse "<raw>": invalid URL escape "%xx"`.
func main() {
	cases := []string{
		"https://example.com/p/a%20th?q=1",
		"https://example.com/a%2Fb/c",
		"http://x/%E4%B8%96%E7%95%8C",
		"/foo/bar%20baz",
		"https://x/path?q=a%20b",
		"http://h/a+b/c%2Bd",
		"https://x/normal/path",
		"mailto:a@b.com",
		"http://x/a%2Fb/c%2Dd",
		"http://x/%41%42",
		"http://x/a/./b/../c",
		"https://h/p%C3%A9",
		"/p/a%th", // malformed escape
		"/p/a%t",
	}
	for _, raw := range cases {
		u, err := url.Parse(raw)
		if err != nil {
			fmt.Printf("%q -> ERR %v\n", raw, err)
			continue
		}
		fmt.Printf("%q -> Path=%q Esc=%q Str=%q Req=%q\n", raw, u.Path, u.EscapedPath(), u.String(), u.RequestURI())
	}

	base, _ := url.Parse("http://x/a/b/c%20d")
	ref, _ := url.Parse("../e%20f")
	fmt.Println(base.ResolveReference(ref).String())

	u3 := &url.URL{Scheme: "http", Host: "x"}
	u3.Path = "/hello world/x"
	fmt.Println(u3.String(), u3.EscapedPath())

	out, _ := url.JoinPath("http://x/base", "a b", "c")
	fmt.Println(out)
}
