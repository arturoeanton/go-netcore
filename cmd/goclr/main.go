// Command goclr is an experimental compiler that builds pure-Go projects into
// .NET assemblies. See docs/ROADMAP.md for the implemented surface vs. the milestones
// that are still in progress.
package main

import (
	"os"

	"github.com/arturoeanton/go-netcore/internal/cli"
)

func main() {
	os.Exit(cli.Run(os.Args[1:]))
}
