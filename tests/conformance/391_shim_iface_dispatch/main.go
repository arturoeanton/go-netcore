// A method called through an interface on an opaque shim value dispatches to the shim's
// method — agnostically, via types.Implements + the [GoShim] registry, with no type
// hardcoded in the compiler. Covers os.Signal.String() and sync.Locker.Lock/Unlock,
// including the RWMutex returned by RLocker().
package main

import (
	"fmt"
	"os"
	"sync"
	"syscall"
)

func lockUnlock(l sync.Locker) { l.Lock(); l.Unlock() }

func main() {
	var s os.Signal = syscall.SIGTERM
	fmt.Println("signal:", s.String())
	for _, x := range []os.Signal{syscall.SIGINT, os.Interrupt, syscall.SIGHUP} {
		fmt.Println("  ", x.String())
	}

	var mu sync.Mutex
	lockUnlock(&mu)
	var rw sync.RWMutex
	lockUnlock(&rw)
	lockUnlock(rw.RLocker())
	fmt.Println("locks ok")
}
