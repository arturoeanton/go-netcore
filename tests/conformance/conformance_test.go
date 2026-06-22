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
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strings"
	"sync"
	"testing"
)

// fixtureResult records one fixture's outcome for the published conformance matrix.
type fixtureResult struct {
	name   string
	status string // "pass", "skip", "fail"
	detail string
}

var (
	resultsMu sync.Mutex
	results   []fixtureResult
)

func recordResult(name, status, detail string) {
	resultsMu.Lock()
	results = append(results, fixtureResult{name, status, detail})
	resultsMu.Unlock()
}

// writeMatrix emits a per-fixture status table to GITHUB_STEP_SUMMARY (or the file named
// by GOCLR_CONFORMANCE_SUMMARY), so CI publishes a visible conformance matrix rather than a
// single pass/fail. No-op when neither is set (local runs).
func writeMatrix() {
	path := os.Getenv("GOCLR_CONFORMANCE_SUMMARY")
	if path == "" {
		path = os.Getenv("GITHUB_STEP_SUMMARY")
	}
	if path == "" || len(results) == 0 {
		return
	}
	sort.Slice(results, func(i, j int) bool { return results[i].name < results[j].name })
	var pass, skip, fail int
	for _, r := range results {
		switch r.status {
		case "pass":
			pass++
		case "skip":
			skip++
		case "fail":
			fail++
		}
	}
	var b strings.Builder
	fmt.Fprintf(&b, "## Conformance matrix (`go run` vs `goclr run`)\n\n")
	fmt.Fprintf(&b, "**%d passed · %d skipped · %d failed** of %d fixtures.\n\n", pass, skip, fail, len(results))
	fmt.Fprintf(&b, "| Fixture | Status |\n|---|---|\n")
	for _, r := range results {
		icon := map[string]string{"pass": "✅ pass", "skip": "⏭️ skip", "fail": "❌ fail"}[r.status]
		fmt.Fprintf(&b, "| %s | %s |\n", r.name, icon)
	}
	f, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		return
	}
	defer f.Close()
	_, _ = f.WriteString(b.String())
}

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
				recordResult(name, "skip", "outside current backend subset")
				t.Skipf("outside current backend subset:\n%s", strings.TrimSpace(clrStderr))
			}

			failed := false
			if clrCode != goCode {
				failed = true
				t.Errorf("exit code mismatch: go=%d goclr=%d\ngoclr stderr:\n%s", goCode, clrCode, clrStderr)
			}
			if clrOut != goOut {
				failed = true
				t.Errorf("output mismatch:\n--- go ---\n%q\n--- goclr ---\n%q", goOut, clrOut)
			}
			if failed {
				recordResult(name, "fail", "")
			} else {
				recordResult(name, "pass", "")
			}
			ran++
		})
	}
	writeMatrix()
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
	// Use CombinedOutput so stdout/stderr interleave in real (program) order, the
	// same way the `go run` baseline is captured — otherwise a program mixing
	// fmt (stdout) and println (stderr) would compare against a reordered stream.
	cmd := exec.Command(goclr, "run", pkg)
	out, err := cmd.CombinedOutput()
	return string(out), exitCode(err), string(out)
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
