package main

import (
	"fmt"
	"time"
)

// time.Month / time.Weekday / time.Duration are named scalar types whose String()
// is dispatched by fmt (Println / %v / %s) just like a user Stringer, not printed as
// the raw underlying integer.
func main() {
	t := time.Date(2024, time.March, 5, 14, 30, 45, 0, time.UTC)
	fmt.Println(t.Month(), t.Weekday())
	fmt.Printf("%v %v\n", t.Month(), t.Weekday())
	d := 90*time.Minute + 30*time.Second
	fmt.Println(d)
	fmt.Printf("%v %s\n", d, d)
	fmt.Println(time.Sunday, time.Saturday)
	fmt.Printf("month=%v weekday=%v dur=%v\n", time.January, time.Friday, time.Hour)
}
