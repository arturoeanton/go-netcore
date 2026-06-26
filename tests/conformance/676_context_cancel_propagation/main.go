package main

import (
	"context"
	"errors"
	"fmt"
)

// Cancelling a parent context must close the Done channel of every cancelable
// descendant, not just the parent's own. Previously a child created with
// WithCancel/WithTimeout kept its own Done channel that never closed when an
// ancestor was cancelled, so <-child.Done() blocked forever (Err() walked the
// parent chain and worked, but Done() could not lazily unblock a real channel).
func main() {
	// direct child: cancel parent -> child Done closes, Err propagates
	pctx, pcancel := context.WithCancel(context.Background())
	cctx, _ := context.WithCancel(pctx)
	pcancel()
	<-cctx.Done()
	fmt.Println("child:", cctx.Err(), errors.Is(cctx.Err(), context.Canceled))

	// grandchild through an intervening WithValue (non-cancelable) layer
	gp, gpcancel := context.WithCancel(context.Background())
	mid := context.WithValue(gp, "k", "v")
	gc, _ := context.WithCancel(mid)
	gpcancel()
	<-gc.Done()
	fmt.Println("grandchild:", gc.Err())

	// cancelling the child must NOT cancel the parent
	p2, p2cancel := context.WithCancel(context.Background())
	defer p2cancel()
	c2, c2cancel := context.WithCancel(p2)
	c2cancel()
	<-c2.Done()
	select {
	case <-p2.Done():
		fmt.Println("parent wrongly cancelled")
	default:
		fmt.Println("parent still alive:", p2.Err())
	}

	// already-cancelled parent: a child created afterwards is born done
	p3, p3cancel := context.WithCancel(context.Background())
	p3cancel()
	c3, _ := context.WithCancel(p3)
	<-c3.Done()
	fmt.Println("born-done:", c3.Err())

	// WithCancelCause propagates the parent's cause to the child's Cause()
	p4, p4cancel := context.WithCancelCause(context.Background())
	c4, _ := context.WithCancel(p4)
	p4cancel(errors.New("boom"))
	<-c4.Done()
	fmt.Println("cause:", c4.Err(), context.Cause(c4))

	// WithoutCancel severs propagation: parent cancel leaves the child alive
	p5, p5cancel := context.WithCancel(context.Background())
	stop := context.WithoutCancel(p5)
	c5, c5cancel := context.WithCancel(stop)
	defer c5cancel()
	p5cancel()
	select {
	case <-c5.Done():
		fmt.Println("severed child wrongly cancelled")
	default:
		fmt.Println("severed child alive:", c5.Err())
	}
}
