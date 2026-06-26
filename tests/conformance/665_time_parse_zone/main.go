package main

import (
	"fmt"
	"time"
)

// time.Parse reads a zone offset from the input (Z07:00 / -0700) into the value's zone, and
// ParseInLocation interprets a zoneless layout in the given location. The stored instant is
// UTC; Format reproduces the offset.
func main() {
	t, _ := time.Parse("2006-01-02T15:04:05Z07:00", "2024-03-15T14:30:00+05:30")
	fmt.Println(t.Format("2006-01-02 15:04:05 -0700"), t.UTC().Format("15:04"))

	t2, _ := time.Parse(time.RFC3339, "2024-03-15T14:30:00-08:00")
	fmt.Println(t2.UTC().Format("15:04:05"), t2.Format("-0700"))

	t3, _ := time.Parse(time.RFC3339, "2024-03-15T14:30:00Z")
	fmt.Println(t3.Format("-0700"), t3.Format("Z07:00"), t3.UTC().Format("15:04"))

	// no zone in layout -> UTC
	t4, _ := time.Parse("2006-01-02 15:04:05", "2024-01-01 12:00:00")
	fmt.Println(t4.Format("15:04:05 -0700"))

	// ParseInLocation interprets the wall clock in loc
	ist := time.FixedZone("IST", 5*3600+1800)
	t5, _ := time.ParseInLocation("2006-01-02 15:04:05", "2024-06-01 09:00:00", ist)
	fmt.Println(t5.Format("15:04:05 -0700"), t5.UTC().Format("15:04"))

	// round-trip
	t6, _ := time.Parse(time.RFC3339, "2024-12-25T08:00:00-05:00")
	fmt.Println(t6.Format(time.RFC3339))
}
