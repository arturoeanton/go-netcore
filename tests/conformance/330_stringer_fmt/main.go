package main

import "fmt"

type Point struct{ X, Y int }

func (p Point) String() string { return fmt.Sprintf("(%d,%d)", p.X, p.Y) }

type Money struct{ cents int }

func (m *Money) String() string { return fmt.Sprintf("$%d.%02d", m.cents/100, m.cents%100) }

type NotFound struct{ Key string }

func (e *NotFound) Error() string { return "not found: " + e.Key }

func main() {
	// value-receiver Stringer: value and pointer both format via String()
	p := Point{3, 4}
	fmt.Println(p, &p)
	fmt.Printf("%v | %s | %+v\n", p, p, p)

	// pointer-receiver Stringer
	m := &Money{1299}
	fmt.Println(m)
	fmt.Printf("%v %s\n", m, m)

	// error interface dispatches Error()
	var err error = &NotFound{"user42"}
	fmt.Println(err)
	fmt.Printf("wrapped: %v\n", fmt.Errorf("op failed: %w", err))

	// Stringer inside composite values
	pts := []Point{{1, 1}, {2, 2}}
	fmt.Println(pts)
	byName := map[string]Point{"origin": {0, 0}}
	fmt.Println(byName["origin"])

	// numeric verbs and %#v are NOT governed by String()
	fmt.Printf("%d,%d\n", p.X, p.Y)
	fmt.Printf("%#v\n", p)
}
