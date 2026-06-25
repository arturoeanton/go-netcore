package main

import (
	"fmt"
	"time"
)

// time layout fractional seconds: '.'/',' followed by a run of '0' (fixed width,
// trailing zeros kept) or '9' (trailing zeros trimmed, separator omitted when empty)
// of ANY width — not just the canonical 3/6/9-digit forms. Format and Parse both.
func main() {
	t := time.Date(2026, 3, 14, 15, 9, 26, 535000000, time.UTC)
	zero := time.Date(2026, 3, 14, 15, 9, 26, 0, time.UTC)
	micro := time.Date(2026, 3, 14, 15, 9, 26, 7000, time.UTC) // .000007

	layouts := []string{
		"05.9", "05.99", "05.999", "05.9999",
		"05.0", "05.00", "05.000", "05.000000",
		"05.999999", "05.999999999", "05.000000000",
		"05,000", "05,999",
	}
	for _, lay := range layouts {
		fmt.Printf("%-14s full=%q zero=%q micro=%q\n", lay,
			t.Format(lay), zero.Format(lay), micro.Format(lay))
	}

	// real-world combined layout
	fmt.Println(t.Format("15:04:05.999"))
	fmt.Println(t.Format("2006-01-02T15:04:05.000Z07:00"))

	// Parse round-trips fractional seconds of varying width and ',' separator
	for _, v := range []string{"15:04:05.5", "15:04:05.535", "15:04:05.000007"} {
		p, err := time.Parse("15:04:05.999999999", v)
		fmt.Println(v, "->", p.Format("15:04:05.999999999"), err)
	}
	p, _ := time.Parse("15:04:05.000", "15:04:05.535")
	fmt.Println(p.Nanosecond())
	pc, _ := time.Parse("15:04:05,000", "15:04:05,250")
	fmt.Println(pc.Nanosecond())
}
