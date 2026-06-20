package main

import (
	"encoding/json"
	"fmt"
	"io"
	"strings"
)

func main() {
	d := json.NewDecoder(strings.NewReader(`{"a":1,"b":[true,null,"x"]}`))
	for {
		tok, err := d.Token()
		if err == io.EOF {
			break
		}
		if err != nil {
			fmt.Println("err:", err)
			break
		}
		switch t := tok.(type) {
		case json.Delim:
			fmt.Printf("delim %c\n", t)
		case string:
			fmt.Printf("string %q\n", t)
		case float64:
			fmt.Printf("float %v\n", t)
		case bool:
			fmt.Printf("bool %v\n", t)
		case nil:
			fmt.Println("null")
		default:
			fmt.Printf("other %T\n", t)
		}
	}
}
