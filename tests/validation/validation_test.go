// Package validation runs each real-application example under both `go run` and
// `goclr run` and asserts that combined stdout+stderr and the exit code match.
//
// Unlike the conformance fixtures (which probe individual language/stdlib
// features), these are whole, idiomatic programs in the three target classes —
// business apps, CLI/ETL, and SaaS services — chosen to demonstrate that the
// compiler is application-agnostic: goja is one hard validation target among
// several, not the product.
//
// Each app is its own module (its own go.mod), so the runner sets the working
// directory to the app and compiles the package there. Apps the backend cannot
// yet compile (goja needs the typed-box keystone — see docs/DESIGN-typed-box.md)
// are reported as skipped via the same GCLR-diagnostic detection the conformance
// suite uses, so the suite stays green and starts asserting automatically as the
// backend grows.
//
// The suite is skipped in -short mode and when `dotnet` is unavailable.
package validation

import (
	"context"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

// perAppTimeout bounds each program (one app runs an HTTP server + client).
const perAppTimeout = 90 * time.Second

func TestValidation(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping validation (needs go run + dotnet) in -short mode")
	}
	if _, err := exec.LookPath("dotnet"); err != nil {
		t.Skip("dotnet not available")
	}

	goclr := buildGoclr(t)

	entries, err := os.ReadDir(".")
	if err != nil {
		t.Fatal(err)
	}
	for _, e := range entries {
		if !e.IsDir() {
			continue
		}
		dir := e.Name()
		if _, err := os.Stat(filepath.Join(dir, "main.go")); err != nil {
			continue
		}
		t.Run(dir, func(t *testing.T) {
			goOut, goCode := run(t, dir, "go", "run", ".")

			clrOut, clrCode := run(t, dir, goclr, "run", ".")
			if isUnsupported(clrOut) {
				t.Skipf("outside current backend subset (needs typed-box keystone):\n%s", strings.TrimSpace(clrOut))
			}
			if clrCode != goCode {
				t.Errorf("exit code mismatch: go=%d goclr=%d\ngoclr output:\n%s", goCode, clrCode, clrOut)
			}
			if clrOut != goOut {
				t.Errorf("output mismatch:\n--- go ---\n%q\n--- goclr ---\n%q", goOut, clrOut)
			}
		})
	}
}

// isUnsupported reports whether goclr declined to compile the program (rather than
// producing divergent output), matching the conformance suite's skip policy.
func isUnsupported(out string) bool {
	for _, code := range []string{"GCLR0102", "GCLR0301", "GCLR0401"} {
		if strings.Contains(out, code) {
			return true
		}
	}
	return false
}

// run executes a command in dir, returning combined stdout+stderr and exit code.
func run(t *testing.T, dir, name string, args ...string) (string, int) {
	t.Helper()
	ctx, cancel := context.WithTimeout(context.Background(), perAppTimeout)
	defer cancel()
	cmd := exec.CommandContext(ctx, name, args...)
	cmd.Dir = dir
	out, err := cmd.CombinedOutput()
	if ctx.Err() == context.DeadlineExceeded {
		t.Fatalf("%s timed out after %s\noutput:\n%s", name, perAppTimeout, out)
	}
	return string(out), exitCode(err)
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
