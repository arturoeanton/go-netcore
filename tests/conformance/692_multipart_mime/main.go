package main

import (
	"bytes"
	"fmt"
	"io"
	"mime"
	"mime/multipart"
	"net/textproto"
	"strings"
)

// mime.ParseMediaType/FormatMediaType, multipart Writer (WriteField/CreateFormFile) and
// Reader round-trip, and textproto MIMEHeader. (*multipart.Writer).WriteField returns a
// single error; it was declared as a two-value tuple, so the call failed at runtime with
// a MissingMethodException.
func main() {
	fmt.Println(mime.TypeByExtension(".json"), mime.TypeByExtension(".html"))
	mt, params, _ := mime.ParseMediaType("text/html; charset=utf-8; boundary=xyz")
	fmt.Println(mt, params["charset"], params["boundary"])
	fmt.Println(mime.FormatMediaType("text/plain", map[string]string{"charset": "utf-8"}))

	var buf bytes.Buffer
	w := multipart.NewWriter(&buf)
	w.WriteField("name", "Alice")
	fw, _ := w.CreateFormFile("upload", "test.txt")
	fw.Write([]byte("file content"))
	w.Close()

	r := multipart.NewReader(strings.NewReader(buf.String()), w.Boundary())
	for {
		part, err := r.NextPart()
		if err == io.EOF {
			break
		}
		if err != nil {
			fmt.Println("err", err)
			break
		}
		data, _ := io.ReadAll(part)
		fmt.Printf("part %q file=%q: %q\n", part.FormName(), part.FileName(), data)
	}

	h := textproto.MIMEHeader{}
	h.Set("Content-Type", "text/plain")
	h.Add("X-Tag", "a")
	h.Add("X-Tag", "b")
	fmt.Println(h.Get("Content-Type"), h.Values("X-Tag"))
	fmt.Println(textproto.CanonicalMIMEHeaderKey("content-type"), textproto.CanonicalMIMEHeaderKey("x-custom-header"))
}
