package main

import (
	"fmt"
	"sync"
	"sync/atomic"
)

type point struct{ x, y int }

func setp(p *int, v int) { *p = v }

type ctr struct{ n int64 }

func main() {
	// &field passed to a function that writes through it
	var pt point
	setp(&pt.x, 7)
	setp(&pt.y, 9)
	fmt.Println(pt.x, pt.y)

	// read/write through a field pointer alias
	px := &pt.x
	*px = *px + 100
	fmt.Println(pt.x, *px)

	// &field of a pointer-rooted struct
	q := &point{x: 1, y: 2}
	qy := &q.y
	*qy = 20
	fmt.Println(q.x, q.y)

	// concurrent atomic on a struct field (1000 increments)
	var c ctr
	var wg sync.WaitGroup
	for i := 0; i < 1000; i++ {
		wg.Add(1)
		go func() { defer wg.Done(); atomic.AddInt64(&c.n, 1) }()
	}
	wg.Wait()
	fmt.Println(atomic.LoadInt64(&c.n))
}
