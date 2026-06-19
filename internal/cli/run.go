package cli

import (
	"flag"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
)

func cmdRun(args []string) int {
	fs := flag.NewFlagSet("run", flag.ContinueOnError)
	bf := registerBuildFlags(fs)
	flags, patterns := splitArgs(args, buildValueFlags)
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		fmt.Fprintln(os.Stderr, "goclr run: expected a package pattern, e.g. ./cmd/server")
		return 2
	}

	out := filepath.Join(".goclr", "app.dll")
	code, dll := buildToAssembly(patterns, bf, out)
	if code != 0 {
		return code
	}

	// When the backend produces a real assembly, run it with dotnet.
	cmd := exec.Command("dotnet", dll)
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	if err := cmd.Run(); err != nil {
		if ee, ok := err.(*exec.ExitError); ok {
			return ee.ExitCode()
		}
		fmt.Fprintf(os.Stderr, "goclr run: %v\n", err)
		return 1
	}
	return 0
}
