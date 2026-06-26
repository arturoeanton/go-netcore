package main

import (
	"fmt"
	"time"
)

// Go's reference layout has day-of-year tokens: "002" (zero-padded, width 3) and
// "__2" (space-padded, width 3). Both Format and Parse must honor them.
func main() {
	for _, d := range []time.Time{
		time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC),    // 001
		time.Date(2024, 3, 15, 0, 0, 0, 0, time.UTC),   // 075 (leap year)
		time.Date(2023, 3, 15, 0, 0, 0, 0, time.UTC),   // 074 (non-leap)
		time.Date(2024, 12, 31, 0, 0, 0, 0, time.UTC),  // 366 (leap)
		time.Date(2023, 12, 31, 0, 0, 0, 0, time.UTC),  // 365
		time.Date(2024, 2, 9, 0, 0, 0, 0, time.UTC),    // 040 -> __2 = " 40"
	} {
		fmt.Printf("[%s] [%s] [%s] yday=%d\n",
			d.Format("2006-002"), d.Format("__2"), d.Format("Monday 002"), d.YearDay())
	}

	// Parse round-trip: day-of-year reconstructs the calendar date.
	for _, s := range []string{"2024-075", "2024-001", "2024-366", "2023-365", "2024-040"} {
		t, err := time.Parse("2006-002", s)
		fmt.Println(t.Format("2006-01-02"), err)
	}

	// Space-padded parse.
	t, err := time.Parse("2006 __2", "2024  40")
	fmt.Println(t.Format("2006-01-02"), err)

	// Out-of-range day-of-year errors (366 in a non-leap year).
	_, err = time.Parse("2006-002", "2023-366")
	fmt.Println(err != nil)
}
