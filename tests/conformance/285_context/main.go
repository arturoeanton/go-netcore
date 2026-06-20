package main

import (
	"context"
	"fmt"
)

type key string

func worker(ctx context.Context, done chan int) {
	<-ctx.Done()
	done <- 1
}

func main() {
	ctx := context.WithValue(context.Background(), key("id"), 7)
	fmt.Println(ctx.Value(key("id")))
	fmt.Println(ctx.Value(key("nope")))

	cctx, cancel := context.WithCancel(ctx)
	fmt.Println(cctx.Err())
	fmt.Println(cctx.Value(key("id"))) // value propagates through cancel ctx

	done := make(chan int)
	go worker(cctx, done)
	cancel()
	<-done
	fmt.Println(cctx.Err())
	fmt.Println(cctx.Err() == context.Canceled)

	// select with a cancelled Done
	select {
	case <-cctx.Done():
		fmt.Println("done fired")
	default:
		fmt.Println("default")
	}
}
