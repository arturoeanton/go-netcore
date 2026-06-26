package main

import (
	"fmt"
	"time"
)

// Regression for DST-aware IANA time zones. A *Location loaded from the system
// zoneinfo must resolve its UTC offset and abbreviation per-instant: in July the
// US zones are on daylight time (EDT/-0400), in January on standard time
// (EST/-0500). The offset feeds the -0700 / Z07:00 tokens and all wall-clock
// math (Sub across zones), while the abbreviation feeds the MST token.
func main() {
	zones := []string{
		"America/New_York", "America/Los_Angeles", "America/Chicago",
		"Europe/London", "Asia/Tokyo", "Asia/Kolkata", "Australia/Sydney",
	}
	for _, z := range zones {
		loc, err := time.LoadLocation(z)
		if err != nil {
			fmt.Println(z, "LOAD-ERR", err)
			continue
		}
		summer := time.Date(2024, 7, 15, 12, 0, 0, 0, loc)
		winter := time.Date(2024, 1, 15, 12, 0, 0, 0, loc)
		fmt.Printf("%-20s summer=%s  winter=%s\n",
			z,
			summer.Format("2006-01-02 15:04 -0700 MST"),
			winter.Format("2006-01-02 15:04 -0700 MST"))
	}

	// The same instant displayed in different zones (In) and the RFC3339 offset.
	t := time.Date(2024, 7, 4, 16, 0, 0, 0, time.UTC)
	ny, _ := time.LoadLocation("America/New_York")
	la, _ := time.LoadLocation("America/Los_Angeles")
	fmt.Println(t.Format(time.RFC3339))
	fmt.Println(t.In(ny).Format(time.RFC3339))
	fmt.Println(t.In(la).Format(time.RFC3339))
	fmt.Println(t.In(ny).Location().String(), t.In(la).Location().String())

	// Zone() returns the abbreviation and offset seconds.
	name, off := t.In(ny).Zone()
	fmt.Println(name, off)

	// Wall-clock math is unaffected by the display zone: a fixed instant difference.
	a := time.Date(2024, 1, 1, 0, 0, 0, 0, ny) // EST -0500
	b := time.Date(2024, 7, 1, 0, 0, 0, 0, ny) // EDT -0400
	fmt.Println(b.Sub(a).Round(time.Hour))

	// AddDate across the spring-forward boundary keeps the zone and re-resolves DST.
	mar := time.Date(2024, 3, 1, 12, 0, 0, 0, ny) // EST
	apr := mar.AddDate(0, 1, 0)                    // April -> EDT
	fmt.Println(mar.Format("01-02 -0700 MST"), "->", apr.Format("01-02 -0700 MST"))

	// UTC and a fixed zone are unchanged by all of this.
	fmt.Println(time.Date(2024, 7, 15, 12, 0, 0, 0, time.UTC).Format("15:04 -0700 MST"))
	fz := time.FixedZone("ABC", -3*3600-1800)
	fmt.Println(time.Date(2024, 7, 15, 12, 0, 0, 0, fz).Format("15:04 -0700 MST"))
}
