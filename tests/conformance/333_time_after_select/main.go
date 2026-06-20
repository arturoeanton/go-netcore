package main

import (
	"fmt"
	"time"
)

func main() {
	// The data arrives well before the 1s timeout, so this is deterministic.
	ready := make(chan int, 1)
	ready <- 7
	select {
	case v := <-ready:
		fmt.Println("ready", v)
	case <-time.After(time.Second):
		fmt.Println("timeout")
	}

	// This channel never sends, so the short timeout always wins.
	never := make(chan int)
	select {
	case <-never:
		fmt.Println("never")
	case <-time.After(10 * time.Millisecond):
		fmt.Println("timed out")
	}

	// time.After in a loop with a worker that finishes first.
	done := make(chan bool, 1)
	go func() { done <- true }()
	select {
	case <-done:
		fmt.Println("worker done")
	case <-time.After(time.Second):
		fmt.Println("worker timeout")
	}
}
