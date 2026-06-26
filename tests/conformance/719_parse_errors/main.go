package main

import (
	"errors"
	"fmt"
	"strconv"
	"time"
)

// strconv.Parse* return *strconv.NumError and time.Parse returns *time.ParseError;
// both must be recoverable with errors.As, and the NumError wraps ErrSyntax/ErrRange
// so errors.Is works. Previously these were plain errors, so errors.As/Is failed.
func main() {
	// strconv.NumError
	_, e := strconv.Atoi("abc")
	var ne *strconv.NumError
	fmt.Println(e, errors.As(e, &ne))
	fmt.Println(ne.Func, ne.Num, ne.Err == strconv.ErrSyntax)
	fmt.Println(errors.Is(e, strconv.ErrSyntax))

	_, e2 := strconv.ParseInt("99999999999999999999", 10, 64)
	fmt.Println(errors.Is(e2, strconv.ErrRange))
	var ne2 *strconv.NumError
	errors.As(e2, &ne2)
	fmt.Println(ne2.Func, ne2.Err == strconv.ErrRange)

	for _, s := range []string{"1.2.3", "0xZZ", "", "  5"} {
		_, err := strconv.ParseFloat(s, 64)
		fmt.Printf("%v | syntax=%t\n", err, errors.Is(err, strconv.ErrSyntax))
	}

	// time.ParseError
	cases := []struct{ layout, value string }{
		{"2006-01-02", "not-a-date"},
		{"2006-01-02", "2024-13-01"},      // month out of range
		{"15:04", "25:99"},                // hour out of range
		{"2006-01-02", "2024-01-02extra"}, // extra text
		{time.RFC3339, "2024-06-15T12:00:00Z"},
	}
	for _, c := range cases {
		_, err := time.Parse(c.layout, c.value)
		if err == nil {
			fmt.Println("parsed ok")
			continue
		}
		var pe *time.ParseError
		ok := errors.As(err, &pe)
		fmt.Printf("%v | As=%t Layout=%q Value=%q\n", err, ok, pe.Layout, pe.Value)
	}
}
