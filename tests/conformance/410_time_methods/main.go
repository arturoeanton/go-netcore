package main

import (
	"fmt"
	"time"
)

func main() {
	t := time.Date(2026, time.June, 22, 14, 30, 15, 0, time.UTC)

	y, mo, d := t.Date()
	fmt.Printf("Date: %d-%02d-%02d\n", y, int(mo), d)

	h, mi, s := t.Clock()
	fmt.Printf("Clock: %02d:%02d:%02d\n", h, mi, s)

	// AddDate: calendar arithmetic.
	t2 := t.AddDate(1, 2, 10) // +1y +2mo +10d
	fmt.Println("AddDate:", t2.Format("2006-01-02"))

	// Crossing a month boundary.
	t3 := t.AddDate(0, 0, 12) // June 22 + 12 = July 4
	fmt.Println("AddDate cross:", t3.Format("2006-01-02"))
}
