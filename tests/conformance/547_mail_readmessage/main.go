package main

import (
	"fmt"
	"io"
	"io/fs"
	"net/mail"
	"path/filepath"
	"strings"
)

func main() {
	raw := "From: Alice <alice@example.com>\r\n" +
		"To: Bob <bob@example.com>, carol@example.com\r\n" +
		"Subject: Greetings\r\n" +
		"X-Folded: line one\r\n" +
		"  continued\r\n" +
		"\r\n" +
		"This is the body.\nSecond line.\n"

	msg, err := mail.ReadMessage(strings.NewReader(raw))
	if err != nil {
		fmt.Println("err:", err)
		return
	}
	fmt.Printf("subject=%q from=%q folded=%q\n",
		msg.Header.Get("Subject"), msg.Header.Get("From"), msg.Header.Get("X-Folded"))

	to, _ := msg.Header.AddressList("To")
	for _, a := range to {
		fmt.Printf("to: name=%q addr=%q\n", a.Name, a.Address)
	}

	body, _ := io.ReadAll(msg.Body)
	fmt.Printf("body=%q\n", string(body))

	// AddressList on a missing header → ErrHeaderNotPresent.
	_, err = msg.Header.AddressList("Cc")
	fmt.Println("cc err:", err)

	// io/fs + filepath sentinels.
	fmt.Println(fs.SkipDir, fs.SkipAll)
	fmt.Println("filepath alias:", filepath.SkipDir == fs.SkipDir)
}
