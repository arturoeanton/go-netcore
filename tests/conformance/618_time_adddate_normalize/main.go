package main

import (
	"fmt"
	"time"
)

// time.AddDate adds the calendar components then NORMALIZES (like time.Date): an
// out-of-range result rolls over — Jan 31 + 1 month = Feb 31 -> Mar 3 — it does
// NOT clamp to the month's last day (the .NET AddMonths behaviour). Months and days
// over/underflow into adjacent months/years; sub-second precision is preserved.
func main() {
	d := func(y, m, dd, h, mi, s, ns int) time.Time {
		return time.Date(y, time.Month(m), dd, h, mi, s, ns, time.UTC)
	}
	type C struct {
		t          time.Time
		y, m, days int
	}
	cases := []C{
		{d(2026, 1, 31, 12, 30, 45, 0), 0, 1, 0},   // Feb 31 -> Mar 3
		{d(2026, 1, 31, 0, 0, 0, 0), 0, 13, 0},     // +13 months
		{d(2026, 1, 31, 0, 0, 0, 0), 0, -1, 0},     // -> Dec 31 2025
		{d(2026, 1, 31, 0, 0, 0, 0), 0, -2, 0},     // Nov 31 -> Dec 1
		{d(2026, 3, 31, 0, 0, 0, 0), 0, -1, 0},     // Feb 31 -> Mar 3
		{d(2024, 2, 29, 0, 0, 0, 0), 1, 0, 0},      // leap day +1y -> 2025-03-01
		{d(2026, 12, 15, 0, 0, 0, 0), 0, 1, 0},     // year rollover
		{d(2026, 1, 15, 0, 0, 0, 0), 0, 0, 400},    // +400 days
		{d(2026, 8, 31, 0, 0, 0, 0), 0, 6, 0},      // -> Mar 3 2027
		{d(2026, 1, 31, 23, 59, 59, 123456789), 0, 1, 0}, // sub-second preserved
	}
	for _, c := range cases {
		r := c.t.AddDate(c.y, c.m, c.days)
		fmt.Println(r.Format("2006-01-02 15:04:05.999999999"))
	}
	// Round-trip: AddDate by +1 month then -1 month is not always identity (Go semantics)
	base := time.Date(2026, 1, 31, 0, 0, 0, 0, time.UTC)
	fmt.Println(base.AddDate(0, 1, 0).AddDate(0, -1, 0).Format("2006-01-02"))
}
