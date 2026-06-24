package main

import (
	"bytes"
	"fmt"
	"bufio"
	"net/textproto"
	"strings"
)

func main() {
	// ReadMIMEHeader with a continuation (folded) line.
	hdr := "From: a@b.com\r\n" +
		"Subject: hello\r\n" +
		" world\r\n" +
		"X-Tag: one\r\n" +
		"X-Tag: two\r\n" +
		"\r\n"
	rm := textproto.NewReader(bufioOf(hdr))
	h, err := rm.ReadMIMEHeader()
	fmt.Printf("err=%v subject=%q from=%q xtag=%v\n", err, h.Get("Subject"), h.Get("From"), h.Values("X-Tag"))

	// ReadResponse: a multi-line SMTP-style 250 response.
	resp := "250-first line\r\n250-second line\r\n250 done\r\n"
	rr := textproto.NewReader(bufioOf(resp))
	code, msg, err := rr.ReadResponse(25)
	fmt.Printf("code=%d msg=%q err=%v\n", code, msg, err)

	// ReadDotLines / dot-encoding (leading-dot unescape, "." terminator).
	dot := "line one\r\n..dotted\r\nlast\r\n.\r\n"
	rd := textproto.NewReader(bufioOf(dot))
	lines, err := rd.ReadDotLines()
	fmt.Printf("lines=%v err=%v\n", lines, err)

	// Writer.PrintfLine + DotWriter round-trip.
	var out bytes.Buffer
	w := textproto.NewWriter(bufio.NewWriter(&out))
	w.PrintfLine("GREET %s %d", "hi", 7)
	dw := w.DotWriter()
	dw.Write([]byte("body line\r\n.hidden leading dot\r\n"))
	dw.Close()
	fmt.Printf("written=%q\n", out.String())

	// ReadCodeLine error path: wrong expected code yields a *textproto.Error.
	re := textproto.NewReader(bufioOf("500 boom\r\n"))
	_, _, err = re.ReadCodeLine(2)
	if te, ok := err.(*textproto.Error); ok {
		fmt.Printf("code=%d msg=%q err=%q\n", te.Code, te.Msg, te.Error())
	}
}

func bufioOf(s string) *bufio.Reader { return bufio.NewReader(strings.NewReader(s)) }
