// os/signal: signal constants render by name, a *syscall.Signal stringifies, and
// Notify/Stop/Reset/Ignore register and unregister without disturbing a program that
// never receives a signal. (Real delivery is exercised manually, not here.)
package main

import (
	"fmt"
	"os"
	"os/signal"
	"syscall"
)

func main() {
	fmt.Println(os.Interrupt, os.Kill)
	fmt.Println(syscall.SIGINT, syscall.SIGTERM, syscall.SIGHUP, syscall.SIGQUIT)
	fmt.Println("string:", syscall.SIGTERM.String())

	c := make(chan os.Signal, 1)
	signal.Notify(c, os.Interrupt, syscall.SIGTERM)
	signal.Ignore(syscall.SIGHUP)
	signal.Stop(c)
	signal.Reset()
	fmt.Println("done")
}
