package main

import (
	"fmt"
	"time"
)

// time.FixedZone / non-UTC locations: Date interprets the wall-clock fields in the zone,
// Format renders the offset (-0700/Z07:00) and zone name (MST), UTC()/In() convert the
// instant, Zone() reports name+offset, and Add/AddDate/Truncate preserve the zone. The
// underlying instant (Unix) is zone-independent. UTC times are unchanged.
func main() {
	ist := time.FixedZone("IST", 5*3600+1800) // +05:30
	t := time.Date(2024, 3, 15, 9, 0, 0, 0, ist)
	fmt.Println(t.Format("2006-01-02 15:04:05 -0700 MST"))
	fmt.Println(t.Format(time.RFC3339))
	fmt.Println(t.UTC().Format("15:04:05"))
	fmt.Println(t.Unix())
	name, off := t.Zone()
	fmt.Println(name, off)
	fmt.Println(t.Hour(), t.Minute(), t.Day(), t.Location())

	utc := time.Date(2024, 3, 15, 12, 0, 0, 0, time.UTC)
	fmt.Println(utc.In(ist).Format("15:04 -0700 MST"))

	fmt.Println(t.Add(2*time.Hour).Format("15:04 MST"))
	fmt.Println(t.AddDate(0, 0, 20).Format("2006-01-02 15:04 -0700"))
	fmt.Println(t.Truncate(time.Hour).Format("15:04 -0700"))

	pst := time.FixedZone("PST", -8*3600)
	fmt.Println(time.Date(2024, 12, 25, 0, 0, 0, 0, pst).Format("2006-01-02 15:04:05 Z07:00"))
	fmt.Println(time.Date(2024, 12, 25, 0, 0, 0, 0, pst).UTC().Format("2006-01-02 15:04 Z07:00"))

	// UTC behavior is unchanged.
	u := time.Date(2024, 6, 1, 10, 30, 0, 0, time.UTC)
	fmt.Println(u.Format("2006-01-02 15:04:05 -0700 MST"), u.Hour(), u.Format(time.RFC3339))
}
