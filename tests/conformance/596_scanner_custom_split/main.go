package main

import (
	"bufio"
	"fmt"
	"strings"
)

// bufio.Scanner with a user SplitFunc runs the split protocol: call split(data, atEOF)
// -> (advance, token, err), advancing the cursor and yielding each token.
func main() {
	// split on commas
	sc := bufio.NewScanner(strings.NewReader("a,b,c,d"))
	sc.Split(func(data []byte, atEOF bool) (int, []byte, error) {
		for i, b := range data {
			if b == ',' {
				return i + 1, data[:i], nil
			}
		}
		if atEOF && len(data) > 0 {
			return len(data), data, nil
		}
		return 0, nil, nil
	})
	var parts []string
	for sc.Scan() {
		parts = append(parts, sc.Text())
	}
	fmt.Println(parts)

	// fixed 3-byte chunks
	sc2 := bufio.NewScanner(strings.NewReader("abcdefghij"))
	sc2.Split(func(data []byte, atEOF bool) (advance int, token []byte, err error) {
		if len(data) >= 3 {
			return 3, data[:3], nil
		}
		if atEOF && len(data) > 0 {
			return len(data), data, nil
		}
		return 0, nil, nil
	})
	for sc2.Scan() {
		fmt.Printf("%q ", sc2.Text())
	}
	fmt.Println()

	// keep empty fields (split on ',' including trailing empties), valid at EOF
	sc3 := bufio.NewScanner(strings.NewReader("x,,y,"))
	sc3.Split(func(data []byte, atEOF bool) (int, []byte, error) {
		for i := 0; i < len(data); i++ {
			if data[i] == ',' {
				return i + 1, data[:i], nil
			}
		}
		if atEOF && len(data) > 0 {
			return len(data), data, nil
		}
		return 0, nil, nil
	})
	var got []string
	for sc3.Scan() {
		got = append(got, sc3.Text())
	}
	fmt.Printf("%q\n", got)

	// built-in splits still work after the custom-split changes
	for _, m := range []bufio.SplitFunc{bufio.ScanWords, bufio.ScanLines, bufio.ScanRunes, bufio.ScanBytes} {
		s := bufio.NewScanner(strings.NewReader("hi yo\nbye"))
		s.Split(m)
		n := 0
		for s.Scan() {
			n++
		}
		fmt.Print(n, " ")
	}
	fmt.Println()
}
