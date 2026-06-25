package main

import (
	"fmt"
	"html"
)

// html.UnescapeString: full HTML5 named character references (the with/without-semicolon
// distinction, two-rune replacements), decimal and hex numeric references, and the
// Windows-1252 / U+FFFD fix-ups for invalid code points. Round-trips EscapeString output too.
func main() {
	cases := []string{
		"&lt;b&gt;&amp;&#65;", "&#65;&#x41;&#x263A;", "&copy; &reg; &trade;",
		"&nbsp;x&hellip;", "AT&amp;T &amp; more", "&amp", "&ampersand", "&notit;",
		"&NotEqualTilde;", "&#xFFFF;", "&#0;", "&#xD800;", "&#128512;", "&#x1F600;",
		"&unknown;", "&lt", "&", "&;", "a &amp;&amp; b", "&CounterClockwiseContourIntegral;",
		"100% &lt; 200", "&#9;tab", "Caf&eacute;", "&frac12; &plusmn; &deg;",
		"&#x80;&#x99;", "no entities here", "&nsubset;",
	}
	for _, c := range cases {
		fmt.Printf("%q -> %q\n", c, html.UnescapeString(c))
	}

	// EscapeString round-trips through UnescapeString.
	for _, s := range []string{`<a href="x">&'</a>`, "plain", "a & b < c > d"} {
		e := html.EscapeString(s)
		fmt.Printf("%q -> %q -> %q\n", s, e, html.UnescapeString(e))
	}
}
