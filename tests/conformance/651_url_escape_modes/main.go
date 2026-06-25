package main

import (
	"fmt"
	"net/url"
)

// net/url escaping modes differ in which reserved chars stay unescaped: PathEscape
// (encodePathSegment keeps $&+:=@, escapes /;,?), QueryEscape (escapes all reserved, space->+),
// EscapedFragment (keeps all reserved, escapes space/#), Userinfo (escapes @/?:), and the
// path (encodePath keeps all but ?). URL.String() escapes the fragment too.
func main() {
	specials := "azAZ09 -_.~$&+,/:;=?@#<>\"'%{}|\\^[]"
	fmt.Printf("PathEscape:  %s\n", url.PathEscape(specials))
	fmt.Printf("QueryEscape: %s\n", url.QueryEscape(specials))

	u := &url.URL{Scheme: "http", Host: "x", Path: "/p"}
	u.Fragment = specials
	fmt.Printf("EscFrag:     %s\n", u.EscapedFragment())

	uu := &url.URL{Scheme: "http", Host: "x", User: url.UserPassword(specials, "pw d"), Path: "/"}
	fmt.Printf("Userinfo:    %s\n", uu.String())

	u2 := &url.URL{Scheme: "http", Host: "x"}
	u2.Path = specials
	fmt.Printf("EscPath:     %s\n", u2.EscapedPath())

	// Parse round-trips fragment with literal space and with %xx.
	for _, raw := range []string{
		"http://x/p?a=1#sec tion", "http://x/p#a%20b", "http://x/p#frag$ment&x=y",
		"https://h/%E4%B8%96#%E7%95%8C", "http://x#a+b/c",
	} {
		p, _ := url.Parse(raw)
		fmt.Printf("%q -> Frag=%q Str=%q\n", raw, p.Fragment, p.String())
	}

	// Values.Encode escapes keys and values as query components.
	v := url.Values{}
	v.Add("a b", "1&2")
	v.Add("c/d", "e=f")
	fmt.Println(v.Encode())
}
