package main

import (
	"fmt"
	"mime"
	"net/mail"
	"strings"
)

// net/mail: ParseAddress/ParseAddressList, ReadMessage headers, and ParseDate, plus
// a *mail.Address printing via its String() under fmt (previously it printed the
// struct fields). mime word encode/decode alongside.
func main() {
	addr, err := mail.ParseAddress("Alice <alice@example.com>")
	fmt.Println(addr, err)        // String(): "Alice" <alice@example.com>
	fmt.Println(addr.String())
	fmt.Println(addr.Name, addr.Address)

	addrs, _ := mail.ParseAddressList("bob@x.com, Carol <carol@y.com>")
	fmt.Println(addrs)
	for _, a := range addrs {
		fmt.Printf("%q %q\n", a.Name, a.Address)
	}

	// ParseDate across a few RFC 5322 layouts.
	for _, d := range []string{
		"Mon, 02 Jan 2006 15:04:05 -0700",
		"15 Mar 2024 09:30:00 +0000",
		"Tue, 1 Jan 2019 00:00:00 +0100",
	} {
		t, e := mail.ParseDate(d)
		fmt.Println(t.UTC().Format("2006-01-02 15:04:05 MST"), e)
	}
	_, e := mail.ParseDate("not a date")
	fmt.Println(e != nil)

	// ReadMessage header.
	msg, _ := mail.ReadMessage(strings.NewReader("From: a@b.com\r\nSubject: Hi\r\n\r\nBody"))
	fmt.Println(msg.Header.Get("From"), msg.Header.Get("Subject"))

	// mime word encode/decode.
	fmt.Println(mime.QEncoding.Encode("utf-8", "Héllo Wörld"))
	fmt.Println(mime.BEncoding.Encode("utf-8", "Test 世界"))
	dec := new(mime.WordDecoder)
	d, _ := dec.Decode("=?utf-8?q?H=C3=A9llo?=")
	fmt.Println(d)
}
