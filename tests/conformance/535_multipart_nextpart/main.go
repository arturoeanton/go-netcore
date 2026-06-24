package main

import (
	"fmt"
	"io"
	"mime/multipart"
	"strings"
)

func main() {
	const boundary = "xYzBoundary"
	body := "--xYzBoundary\r\n" +
		"Content-Disposition: form-data; name=\"field1\"\r\n" +
		"\r\n" +
		"value one\r\n" +
		"--xYzBoundary\r\n" +
		"Content-Disposition: form-data; name=\"file1\"; filename=\"/etc/passwd\"\r\n" +
		"Content-Type: text/plain\r\n" +
		"\r\n" +
		"file body here\r\n" +
		"--xYzBoundary\r\n" +
		"Content-Disposition: form-data; name=\"note\"\r\n" +
		"Content-Transfer-Encoding: quoted-printable\r\n" +
		"\r\n" +
		"caf=C3=A9 m=C3=BCller\r\n" +
		"--xYzBoundary--\r\n"

	r := multipart.NewReader(strings.NewReader(body), boundary)
	for {
		p, err := r.NextPart()
		if err == io.EOF {
			break
		}
		if err != nil {
			fmt.Println("err:", err)
			break
		}
		data, _ := io.ReadAll(p)
		fmt.Printf("form=%q file=%q ctype=%q cte=%q body=%q\n",
			p.FormName(), p.FileName(), p.Header.Get("Content-Type"),
			p.Header.Get("Content-Transfer-Encoding"), string(data))
	}
}
