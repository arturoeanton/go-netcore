package main

import "fmt"

type Money int64

func (m Money) String() string { return fmt.Sprintf("$%d.%02d", m/100, m%100) }

type Level int

const (
	Low Level = iota
	Mid
	High
)

func (l Level) String() string {
	return [...]string{"low", "mid", "high"}[l]
}

func describe(v any) string { return fmt.Sprintf("%v (%T)", v, v) }

func main() {
	var m Money = 14997
	fmt.Println(m)
	fmt.Printf("%s | %d | %v | %T\n", m, m, m, m)
	fmt.Println(describe(m))

	lv := Mid
	fmt.Println(Low, lv, High)
	fmt.Printf("%v=%d %T\n", lv, lv, lv)

	// named type passed through an interface and formatted
	vals := []any{Money(500), High, Money(-250)}
	for _, v := range vals {
		fmt.Println(v)
	}
}
