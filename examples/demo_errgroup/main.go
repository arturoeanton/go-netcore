// demo_errgroup shows golang.org/x/sync/errgroup running on the CLR: concurrent goroutines
// coordinated by an errgroup.Group, with error propagation. Requires `go mod vendor`.
package main

import (
	"fmt"
	"sort"
	"sync"

	"golang.org/x/sync/errgroup"
)

func main() {
	var g errgroup.Group
	var mu sync.Mutex
	var squares []int
	for i := 1; i <= 5; i++ {
		i := i
		g.Go(func() error {
			mu.Lock()
			squares = append(squares, i*i)
			mu.Unlock()
			return nil
		})
	}
	if err := g.Wait(); err != nil {
		fmt.Println("unexpected:", err)
		return
	}
	sort.Ints(squares)
	fmt.Println("squares:", squares)

	var g2 errgroup.Group
	g2.Go(func() error { return nil })
	g2.Go(func() error { return fmt.Errorf("task failed") })
	fmt.Println("first error:", g2.Wait())
}
