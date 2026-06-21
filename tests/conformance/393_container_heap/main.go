// container/heap exercised through the interface method-callback bridge: heap.Init/
// Push/Pop drive the user type's Len/Less/Swap/Push/Pop across the shim boundary via
// GoRuntime/Bridge.CallMethod. Struct-receiver implementer (the clean type-id path).
package main

import (
	"container/heap"
	"fmt"
)

type Item struct {
	name string
	prio int
}

type PQ struct{ items []Item }

func (q PQ) Len() int            { return len(q.items) }
func (q PQ) Less(i, j int) bool  { return q.items[i].prio < q.items[j].prio }
func (q PQ) Swap(i, j int)       { q.items[i], q.items[j] = q.items[j], q.items[i] }
func (q *PQ) Push(x any)         { q.items = append(q.items, x.(Item)) }
func (q *PQ) Pop() any {
	old := q.items
	n := len(old)
	it := old[n-1]
	q.items = old[:n-1]
	return it
}

func main() {
	q := &PQ{}
	heap.Init(q)
	for _, it := range []Item{{"c", 3}, {"a", 1}, {"e", 5}, {"b", 2}, {"d", 4}} {
		heap.Push(q, it)
	}
	heap.Push(q, Item{"z", 0})
	for q.Len() > 0 {
		it := heap.Pop(q).(Item)
		fmt.Printf("%s=%d ", it.name, it.prio)
	}
	fmt.Println()

	// Fix after mutating an element's priority.
	q2 := &PQ{}
	for _, it := range []Item{{"x", 10}, {"y", 20}, {"w", 30}} {
		heap.Push(q2, it)
	}
	q2.items[0].prio = 99
	heap.Fix(q2, 0)
	fmt.Println(heap.Pop(q2).(Item).name)
}
