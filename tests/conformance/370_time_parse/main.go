package main

import (
	"fmt"
	"time"
)

// time.Parse: the inverse of Format over Go's reference-time layout.
func main() {
	t, err := time.Parse("2006-01-02 15:04:05", "2023-06-15 14:30:45")
	fmt.Println(t.Year(), int(t.Month()), t.Day(), t.Hour(), t.Minute(), t.Second(), err)

	t2, _ := time.Parse("15:04:05", "00:00:20")
	fmt.Println(t2.Hour(), t2.Minute(), t2.Second())

	t3, _ := time.Parse("Jan 2, 2006", "Jun 15, 2023")
	fmt.Println(t3.Year(), int(t3.Month()), t3.Day())

	_, e := time.Parse("2006", "notayear")
	fmt.Println(e != nil)
}
