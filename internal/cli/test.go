package cli

import (
	"flag"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
)

// cmdTest compiles a package's tests (the synthesized `*.test` runner, driven by goclr's
// real-Go `testing` overlay) to a .NET assembly and runs it, printing a go-test-like
// report and exiting non-zero if any test fails. Supports TestXxx(t *testing.T) with the
// common testing.T surface and subtests; benchmarks/fuzzing/examples/TestMain are not run
// (see docs/LIMITATIONS.md).
func cmdTest(args []string) int {
	fs := flag.NewFlagSet("test", flag.ContinueOnError)
	bf := registerBuildFlags(fs)
	flags, patterns := splitArgs(args, buildValueFlags)
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		patterns = []string{"."}
	}

	out := filepath.Join(".goclr", "pkg.test.dll")
	code, dll := buildToAssemblyMode(patterns, bf, out, true)
	if code != 0 {
		return code
	}

	cmd := exec.Command("dotnet", dll)
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	if err := cmd.Run(); err != nil {
		if ee, ok := err.(*exec.ExitError); ok {
			return ee.ExitCode()
		}
		fmt.Fprintf(os.Stderr, "goclr test: %v\n", err)
		return 1
	}
	return 0
}
