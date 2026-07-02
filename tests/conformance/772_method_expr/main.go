package main

import (
	"fmt"
	"slices"
)

// An interface method expression (Item.Compare) used as a func value — the receiver
// becomes the first argument, dispatched dynamically at call time. This is the
// shape OPA uses: slices.SortFunc(xs, Value.Compare).
type Item interface {
	Compare(Item) int
	Label() string
}

type num struct{ v int }

func (a num) Compare(o Item) int { return a.v - o.(num).v }
func (a num) Label() string      { return fmt.Sprintf("n%d", a.v) }

func main() {
	xs := []Item{num{3}, num{1}, num{2}}

	// Method expression passed directly as the SortFunc comparator.
	slices.SortFunc(xs, Item.Compare)
	for _, x := range xs {
		fmt.Println("sorted:", x.Label())
	}

	// Method expression bound to a var, invoked with an explicit receiver.
	label := Item.Label
	fmt.Println("expr:", label(xs[0]), label(xs[1]), label(xs[2]))
}
