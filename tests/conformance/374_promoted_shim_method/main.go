package main

import (
	"fmt"
	"time"
)

// A method promoted from an embedded shim-type field: clock embeds time.Time, so
// clock.Format resolves to the (shimmed) time.Time.Format, with the embedded field
// as receiver.
type clock struct {
	time.Time
	label string
}

func main() {
	c := clock{Time: time.Date(2023, 6, 15, 9, 30, 0, 0, time.UTC), label: "demo"}
	fmt.Println(c.label, c.Format("2006-01-02 15:04:05"))
	fmt.Println(c.Year(), int(c.Month()), c.Day())
}
