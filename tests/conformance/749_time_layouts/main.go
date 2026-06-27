package main

import (
	"fmt"
	"time"
)

// Every predefined time layout constant formats byte-exact, plus unusual tokens and a
// range of Parse cases. (A time-only Parse defaults the date to 1970 rather than Go's
// 0000 — a documented DateTime-range gap — so the clock, not the date, is checked there.)
func main() {
	t := time.Date(2024, 3, 5, 14, 8, 9, 123456789, time.UTC)
	for _, l := range []string{
		time.Layout, time.ANSIC, time.UnixDate, time.RubyDate, time.RFC822, time.RFC822Z,
		time.RFC850, time.RFC1123, time.RFC1123Z, time.RFC3339, time.RFC3339Nano,
		time.Kitchen, time.Stamp, time.StampMilli, time.StampMicro, time.StampNano,
		time.DateTime, time.DateOnly, time.TimeOnly,
	} {
		fmt.Println(t.Format(l))
	}
	fmt.Println(t.Format("3:04:05.000 PM"))
	fmt.Println(t.Format("Mon, 02 Jan 2006"))
	fmt.Println(t.Format("2006年01月02日"))
	fmt.Println(t.Format("Jan _2 15:04:05.000000"))
	fmt.Println(time.Date(2024, 1, 2, 3, 4, 5, 0, time.UTC).Format("1/2/2006 3:4:5"))

	for _, s := range []struct{ layout, val string }{
		{time.RFC3339, "2024-03-05T14:08:09Z"},
		{time.RFC1123Z, "Tue, 05 Mar 2024 14:08:09 +0000"},
		{"2006-01-02", "2024-03-05"},
		{"01/02/2006 3:04 PM", "03/05/2024 2:08 PM"},
	} {
		p, err := time.Parse(s.layout, s.val)
		fmt.Println(p.Format("2006-01-02 15:04:05"), err)
	}
	// Time-only parse: the clock is exact (the date defaults, documented).
	kt, err := time.Parse(time.Kitchen, "2:08PM")
	fmt.Println(kt.Format("15:04:05"), kt.Hour(), kt.Minute(), err)
}
