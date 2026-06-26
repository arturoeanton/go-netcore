package main

import (
	"fmt"
	"sync"
	"time"
)

// sync.Cond.Wait must atomically release its Locker and block until Signal/Broadcast,
// then re-acquire it. Previously goclr's Wait never released the Locker, so a producer
// holding the same mutex deadlocked. Also exercises sync.Pool whose New returns a []byte
// through a func() any (the result must still assert as []byte).
func main() {
	var mu sync.Mutex
	cond := sync.NewCond(&mu)
	queue := []int{}
	done := make(chan bool)
	go func() {
		for i := 0; i < 5; i++ {
			mu.Lock()
			queue = append(queue, i)
			cond.Signal()
			mu.Unlock()
			time.Sleep(time.Millisecond)
		}
	}()
	sum := 0
	go func() {
		count := 0
		for count < 5 {
			mu.Lock()
			for len(queue) == 0 {
				cond.Wait()
			}
			sum += queue[0]
			queue = queue[1:]
			count++
			mu.Unlock()
		}
		done <- true
	}()
	<-done
	fmt.Println("cond done, sum:", sum)

	// Broadcast wakes all waiters.
	var mu2 sync.Mutex
	c2 := sync.NewCond(&mu2)
	ready := false
	var wg sync.WaitGroup
	woke := 0
	var wm sync.Mutex
	for i := 0; i < 3; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			mu2.Lock()
			for !ready {
				c2.Wait()
			}
			mu2.Unlock()
			wm.Lock()
			woke++
			wm.Unlock()
		}()
	}
	time.Sleep(10 * time.Millisecond)
	mu2.Lock()
	ready = true
	c2.Broadcast()
	mu2.Unlock()
	wg.Wait()
	fmt.Println("broadcast woke:", woke)

	// sync.Pool whose New returns a []byte via func() any
	pool := sync.Pool{New: func() any { return make([]byte, 8) }}
	b := pool.Get().([]byte)
	fmt.Println("pool []byte len:", len(b))
	pool.Put(b)
	fmt.Println("pool reuse len:", len(pool.Get().([]byte)))
}
