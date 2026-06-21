// container/heap with the IDIOMATIC named-slice implementer (type IntHeap []int reached
// as *IntHeap). Works now that struct ids and typed-box named ids share one counter, so
// the &IntHeap{...} pointer carries IntHeap's unified id and the callback bridge resolves
// its Len/Less/Swap/Push/Pop. (Was a runtime error before the type-id unification.)
package main

import (
	"container/heap"
	"fmt"
)

type IntHeap []int

func (h IntHeap) Len() int           { return len(h) }
func (h IntHeap) Less(i, j int) bool { return h[i] < h[j] }
func (h IntHeap) Swap(i, j int)      { h[i], h[j] = h[j], h[i] }
func (h *IntHeap) Push(x any)        { *h = append(*h, x.(int)) }
func (h *IntHeap) Pop() any {
	old := *h
	n := len(old)
	v := old[n-1]
	*h = old[:n-1]
	return v
}

func main() {
	h := &IntHeap{5, 2, 8, 1, 9, 3}
	heap.Init(h)
	heap.Push(h, 0)
	heap.Push(h, 7)
	for h.Len() > 0 {
		fmt.Print(heap.Pop(h).(int), " ")
	}
	fmt.Println()
}
