// Package gotest validates `goclr test`: it compiles a fixture package's tests (driven by
// goclr's real-Go testing overlay) to a .NET assembly, runs it, and checks the report and
// exit code. The fixtures under mixed/ and allpass/ carry `//go:build goclr` so the normal
// toolchain skips them ("no test files"); goclr (which sets the goclr build tag) runs them.
package gotest

import (
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

func TestGoclrTest(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping goclr test (needs dotnet) in -short mode")
	}
	if _, err := exec.LookPath("dotnet"); err != nil {
		t.Skip("dotnet not available")
	}
	goclr := buildGoclr(t)

	t.Run("allpass", func(t *testing.T) {
		out, code := runGoclrTest(t, goclr, "./tests/gotest/allpass")
		if code != 0 {
			t.Errorf("allpass exit = %d, want 0\n%s", code, out)
		}
		for _, want := range []string{"--- PASS: TestDouble", "--- PASS: TestDoubleZero", "PASS"} {
			if !strings.Contains(out, want) {
				t.Errorf("allpass output missing %q\n%s", want, out)
			}
		}
		if strings.Contains(out, "FAIL") {
			t.Errorf("allpass should not report FAIL\n%s", out)
		}
	})

	t.Run("mixed", func(t *testing.T) {
		out, code := runGoclrTest(t, goclr, "./tests/gotest/mixed")
		if code != 1 {
			t.Errorf("mixed exit = %d, want 1\n%s", code, out)
		}
		// passing, failing, skipping, and subtest cases each surface correctly.
		for _, want := range []string{
			"--- PASS: TestAddOK",
			"--- FAIL: TestMulFail",
			"--- FAIL: TestFatalStops",
			"stopping with 42",
			"--- SKIP: TestSkipped",
			"--- FAIL: TestSub",
			"FAIL",
		} {
			if !strings.Contains(out, want) {
				t.Errorf("mixed output missing %q\n%s", want, out)
			}
		}
		// Fatal must abort the test: the line after t.Fatalf must not run.
		if strings.Contains(out, "must not run") {
			t.Errorf("Fatalf did not abort the test\n%s", out)
		}
	})
}

func buildGoclr(t *testing.T) string {
	t.Helper()
	bin := filepath.Join(t.TempDir(), "goclr")
	cmd := exec.Command("go", "build", "-o", bin, "github.com/arturoeanton/go-netcore/cmd/goclr")
	cmd.Dir = repoRoot(t)
	if out, err := cmd.CombinedOutput(); err != nil {
		t.Fatalf("building goclr: %v\n%s", err, out)
	}
	return bin
}

// runGoclrTest runs `goclr test <pattern>` from the repo root, returning combined output
// and exit code.
func runGoclrTest(t *testing.T, goclr, pattern string) (string, int) {
	t.Helper()
	cmd := exec.Command(goclr, "test", pattern)
	cmd.Dir = repoRoot(t)
	out, err := cmd.CombinedOutput()
	code := 0
	if ee, ok := err.(*exec.ExitError); ok {
		code = ee.ExitCode()
	} else if err != nil {
		t.Fatalf("running goclr test: %v\n%s", err, out)
	}
	return string(out), code
}

// repoRoot returns the module root (two levels up from tests/gotest).
func repoRoot(t *testing.T) string {
	t.Helper()
	abs, err := filepath.Abs("../..")
	if err != nil {
		t.Fatal(err)
	}
	return abs
}
