package main

import (
	"fmt"
	"time"
)

// time.Parse error messages match Go: a failed layout token reports the unparsed value
// remainder and the expected token ("cannot parse <rest> as <token>"), and an
// out-of-range field reports "<field> out of range". Previously goclr emitted a bare
// "cannot parse" and accepted out-of-range fields.
func main() {
	cases := []struct{ lay, val string }{
		{"2006-01-02", "not-a-date"},   // fails at year token
		{"2006-01-02", "2024-XX-02"},   // fails at month token
		{"Jan 2, 2006", "Foo 2, 2024"}, // fails at month-name token
		{"15:04:05", "10:99:00"},       // minute out of range
		{"15:04:05", "25:00:00"},       // hour out of range (24h)
		{"3:04 PM", "13:04 PM"},        // hour out of range (12h)
		{"2006-01-02", "2024-13-02"},   // month out of range
		{"2006-01-02", "2024-01-9z"},   // zero-padded "02" needs exactly two digits
		{"2006", "2024xyz"},            // trailing extra text
		{"2006-01-02", "2024-01-02 ok"}, // trailing extra text after a full date
	}
	for _, c := range cases {
		_, err := time.Parse(c.lay, c.val)
		fmt.Println(err)
	}
	// a valid parse still round-trips
	t, err := time.Parse(time.RFC3339, "2024-06-15T13:45:30Z")
	fmt.Println(t.Format("2006-01-02 15:04:05"), err)
}
