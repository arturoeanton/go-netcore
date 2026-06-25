package main

import (
	"context"
	"fmt"
	"time"
)

// context.Context.Deadline() returns (deadline, true) for a WithTimeout/WithDeadline
// context (or any descendant that inherits it) and (zero, false) otherwise. Previously
// Deadline() was unregistered and the tuple destructure nil-panicked.
func main() {
	bg := context.Background()

	// non-deadline contexts -> (zero, false)
	_, ok := bg.Deadline()
	fmt.Println("bg", ok)
	cc, cancel := context.WithCancel(bg)
	defer cancel()
	_, ok = cc.Deadline()
	fmt.Println("cancel", ok)
	vc := context.WithValue(bg, "k", "v")
	_, ok = vc.Deadline()
	fmt.Println("value", ok)

	// WithTimeout -> (deadline, true)
	tc, cancel2 := context.WithTimeout(bg, time.Hour)
	defer cancel2()
	d, ok := tc.Deadline()
	fmt.Println("timeout", ok, !d.IsZero())

	// WithDeadline at an explicit time round-trips within a second
	dl := time.Now().Add(2 * time.Hour)
	dc, cancel3 := context.WithDeadline(bg, dl)
	defer cancel3()
	d2, ok := dc.Deadline()
	fmt.Println("deadline", ok, d2.Sub(dl) < time.Second && dl.Sub(d2) < time.Second)

	// deadline inherited through WithValue / WithCancel children
	child := context.WithValue(tc, "x", 1)
	_, ok = child.Deadline()
	fmt.Println("inherited-value", ok)
	gchild, cancel4 := context.WithCancel(child)
	defer cancel4()
	_, ok = gchild.Deadline()
	fmt.Println("inherited-cancel", ok)

	// nested timeouts: the inner (sooner) deadline is earlier than the outer
	t1, c1 := context.WithTimeout(bg, time.Hour)
	defer c1()
	t2, c2 := context.WithTimeout(t1, time.Minute)
	defer c2()
	di, _ := t2.Deadline()
	do, _ := t1.Deadline()
	fmt.Println("nested-sooner", di.Before(do))
}
