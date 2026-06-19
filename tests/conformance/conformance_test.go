// Package conformance runs each NNN_* fixture under both `go run` and
// `goclr run` and asserts that combined stdout+stderr and the exit code match.
//
// Fixtures outside the currently-implemented backend subset (which `goclr`
// rejects with GCLR0301) are reported as skipped, so the suite stays green and
// automatically starts asserting on each fixture as the backend grows.
//
// The suite is skipped in -short mode and when `dotnet` is unavailable.
package conformance

import (
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

func TestConformance(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping conformance (needs go run + dotnet) in -short mode")
	}
	if _, err := exec.LookPath("dotnet"); err != nil {
		t.Skip("dotnet not available")
	}

	goclr := buildGoclr(t)

	entries, err := os.ReadDir(".")
	if err != nil {
		t.Fatal(err)
	}
	ran := 0
	for _, e := range entries {
		if !e.IsDir() {
			continue
		}
		if _, err := os.Stat(filepath.Join(e.Name(), "main.go")); err != nil {
			continue
		}
		name := e.Name()
		t.Run(name, func(t *testing.T) {
			pkg := "./" + name

			goOut, goCode := run(t, "go", "run", pkg)

			clrOut, clrCode, clrStderr := runGoclr(t, goclr, pkg)
			if strings.Contains(clrStderr, "GCLR0301") {
				t.Skipf("outside current backend subset:\n%s", strings.TrimSpace(clrStderr))
			}

			if clrCode != goCode {
				t.Errorf("exit code mismatch: go=%d goclr=%d\ngoclr stderr:\n%s", goCode, clrCode, clrStderr)
			}
			if clrOut != goOut {
				t.Errorf("output mismatch:\n--- go ---\n%q\n--- goclr ---\n%q", goOut, clrOut)
			}
			ran++
		})
	}
}

// buildGoclr compiles the CLI to a temp binary and returns its path.
func buildGoclr(t *testing.T) string {
	t.Helper()
	bin := filepath.Join(t.TempDir(), "goclr")
	cmd := exec.Command("go", "build", "-o", bin, "github.com/arturoeanton/go-netcore/cmd/goclr")
	if out, err := cmd.CombinedOutput(); err != nil {
		t.Fatalf("building goclr: %v\n%s", err, out)
	}
	return bin
}

// run executes a command, returning combined stdout+stderr and the exit code.
func run(t *testing.T, name string, args ...string) (string, int) {
	t.Helper()
	cmd := exec.Command(name, args...)
	out, err := cmd.CombinedOutput()
	return string(out), exitCode(err)
}

// runGoclr runs `goclr run <pkg>`, returning combined output, exit code, and the
// stderr alone (used to detect unsupported-subset skips).
func runGoclr(t *testing.T, goclr, pkg string) (combined string, code int, stderr string) {
	t.Helper()
	var sout, serr strings.Builder
	cmd := exec.Command(goclr, "run", pkg)
	cmd.Stdout = &sout
	cmd.Stderr = &serr
	err := cmd.Run()
	return sout.String() + serr.String(), exitCode(err), serr.String()
}

func exitCode(err error) int {
	if err == nil {
		return 0
	}
	if ee, ok := err.(*exec.ExitError); ok {
		return ee.ExitCode()
	}
	return -1
}
