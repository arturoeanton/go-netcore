package main

import (
	"fmt"
	"sync"
)

func main() {
	var wg sync.WaitGroup
	var mu sync.Mutex
	total := 0
	for i := 1; i <= 100; i++ {
		wg.Add(1)
		go func(n int) {
			defer wg.Done()
			mu.Lock()
			total += n
			mu.Unlock()
		}(i)
	}
	wg.Wait()
	fmt.Println("total:", total)
	var once sync.Once
	for i := 0; i < 3; i++ {
		once.Do(func() { fmt.Println("once") })
	}
	var m sync.Map
	m.Store("a", 1)
	v, ok := m.Load("a")
	fmt.Println(v, ok)
}
