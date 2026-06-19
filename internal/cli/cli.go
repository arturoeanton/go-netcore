// Package cli implements the goclr command-line interface.
package cli

import (
	"fmt"
	"os"
)

// Version is the goclr CLI version.
const Version = "0.1.0-mvp"

type command struct {
	name    string
	summary string
	run     func(args []string) int
}

func commands() []command {
	return []command{
		{"build", "Compile a Go main package to a .NET assembly (.dll)", cmdBuild},
		{"run", "Compile and run a Go main package", cmdRun},
		{"analyze", "Analyze compatibility of packages with a GoCLR profile", cmdAnalyze},
		{"test", "Compile and run compatible Go tests", cmdTest},
		{"doctor", "Verify the local environment for goclr", cmdDoctor},
		{"clean", "Remove goclr build artifacts", cmdClean},
		{"version", "Print the goclr version", cmdVersion},
	}
}

// Run dispatches to the requested subcommand and returns the process exit code.
func Run(args []string) int {
	if len(args) == 0 {
		usage(os.Stdout)
		return 0
	}
	name := args[0]
	if name == "-h" || name == "--help" || name == "help" {
		usage(os.Stdout)
		return 0
	}
	for _, c := range commands() {
		if c.name == name {
			return c.run(args[1:])
		}
	}
	fmt.Fprintf(os.Stderr, "goclr: unknown command %q\n\n", name)
	usage(os.Stderr)
	return 2
}

func usage(w *os.File) {
	fmt.Fprintf(w, "goclr %s — compile pure-Go projects to .NET assemblies\n\n", Version)
	fmt.Fprintln(w, "Usage:")
	fmt.Fprintln(w, "  goclr <command> [arguments]")
	fmt.Fprintln(w)
	fmt.Fprintln(w, "Commands:")
	for _, c := range commands() {
		fmt.Fprintf(w, "  %-9s %s\n", c.name, c.summary)
	}
	fmt.Fprintln(w)
	fmt.Fprintln(w, "Profile: the MVP targets the echo-goja compatibility profile.")
	fmt.Fprintln(w, "Run 'goclr <command> -h' for command-specific flags.")
}

func cmdVersion(args []string) int {
	fmt.Printf("goclr %s\n", Version)
	return 0
}
