package main

import (
	"encoding/json"
	"fmt"
	"time"
)

// time.Time marshals/unmarshals as its RFC3339 string (Time.MarshalJSON /
// UnmarshalJSON) when it appears as a struct field, slice element, map value, or a
// top-level target — not as the raw runtime struct.
type Record struct {
	Created time.Time  `json:"created"`
	Updated *time.Time `json:"updated,omitempty"`
	Name    string     `json:"name"`
}

func main() {
	t := time.Date(2026, 3, 14, 15, 9, 26, 123456789, time.UTC)

	// struct field round-trip (with nanoseconds)
	b, _ := json.Marshal(t)
	fmt.Println(string(b))
	var t2 time.Time
	json.Unmarshal(b, &t2)
	fmt.Println(t2.Equal(t), t2.Nanosecond())

	// slice of times
	times := []time.Time{
		time.Date(2026, 1, 1, 0, 0, 0, 0, time.UTC),
		time.Date(2026, 6, 15, 12, 30, 0, 0, time.UTC),
	}
	sb, _ := json.Marshal(times)
	fmt.Println(string(sb))
	var decoded []time.Time
	json.Unmarshal(sb, &decoded)
	fmt.Println(len(decoded), decoded[1].Format("2006-01-02 15:04"))

	// struct with multiple time fields + omitempty pointer + MarshalIndent
	upd := time.Date(2026, 2, 2, 2, 2, 2, 0, time.UTC)
	r := Record{Created: time.Date(2026, 1, 1, 0, 0, 0, 0, time.UTC), Updated: &upd, Name: "x"}
	rb, _ := json.MarshalIndent(r, "", "  ")
	fmt.Println(string(rb))

	// decode into a struct, omitted optional pointer stays nil
	var r2 Record
	json.Unmarshal([]byte(`{"created":"2025-12-25T10:30:00Z","name":"y"}`), &r2)
	fmt.Println(r2.Created.Year(), r2.Name, r2.Updated == nil)

	// map[string]time.Time
	m := map[string]time.Time{"start": time.Date(2026, 5, 5, 5, 5, 5, 0, time.UTC)}
	mb, _ := json.Marshal(m)
	fmt.Println(string(mb))

	// top-level Unmarshal into a time.Time (no nanos and with nanos)
	var a, c time.Time
	json.Unmarshal([]byte(`"2026-03-14T15:09:26Z"`), &a)
	json.Unmarshal([]byte(`"2026-03-14T15:09:26.5Z"`), &c)
	fmt.Println(a.Format("15:04:05"), a.Year(), c.Nanosecond())
}
