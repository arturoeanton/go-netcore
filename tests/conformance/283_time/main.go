package main

import (
	"fmt"
	"time"
)

func main() {
	t := time.Date(2025, time.June, 19, 14, 30, 5, 0, time.UTC)
	fmt.Println(t.Format("2006-01-02 15:04:05"))
	fmt.Println(t.Format(time.RFC3339))
	fmt.Println(t.Format("Mon Jan 2 03:04:05 PM"))
	fmt.Println(t.Format("January 2, 2006"))
	fmt.Println(t.Year(), int(t.Month()), t.Day(), t.Hour(), t.Minute(), t.Second())
	fmt.Println(t.Unix(), t.UnixMilli())

	u := t.Add(48 * time.Hour)
	fmt.Println(u.Format("2006-01-02"))
	fmt.Println(u.Sub(t).Hours())
	fmt.Println(t.Before(u), t.After(u), t.Equal(t))

	v := time.Unix(1700000000, 0).UTC()
	fmt.Println(v.Format("2006-01-02 15:04:05"), v.Unix())

	var z time.Time
	fmt.Println(z.IsZero(), t.IsZero())
	fmt.Println(z.Format("2006-01-02 15:04:05"))

	d := 90 * time.Minute
	fmt.Println(d.String(), d.Hours())
}
