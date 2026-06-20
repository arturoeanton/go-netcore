package main

import (
	"fmt"
	"strings"
	"sync"
)

type Counter struct {
	mu sync.Mutex
	n  int
}

func (c *Counter) Inc() { c.mu.Lock(); defer c.mu.Unlock(); c.n++ }
func (c *Counter) Get() int { c.mu.Lock(); defer c.mu.Unlock(); return c.n }

type Registry struct {
	rw   sync.RWMutex
	data map[string]int
	once sync.Once
	log  strings.Builder
}

func main() {
	// concurrent increments through a struct-field mutex
	c := &Counter{}
	var wg sync.WaitGroup
	for i := 0; i < 200; i++ {
		wg.Add(1)
		go func() { defer wg.Done(); c.Inc() }()
	}
	wg.Wait()
	fmt.Println(c.Get())

	// RWMutex, Once, and Builder as struct fields all zero-init correctly
	r := &Registry{data: map[string]int{}}
	r.rw.Lock()
	r.data["x"] = 10
	r.rw.Unlock()
	r.rw.RLock()
	fmt.Println(r.data["x"])
	r.rw.RUnlock()
	for i := 0; i < 3; i++ {
		r.once.Do(func() { r.log.WriteString("init") })
	}
	fmt.Println(r.log.String())

	// value (non-pointer) struct with a mutex field
	var v Counter
	v.Inc()
	v.Inc()
	fmt.Println(v.Get())
}
