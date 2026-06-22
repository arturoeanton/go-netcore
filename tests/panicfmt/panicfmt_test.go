// Package panicfmt checks that an UNCAUGHT panic crashes in Go's shape — `panic: <value>`
// followed by a `goroutine 1 [running]:` header, exit status 2 — rather than a .NET
// unhandled-exception dump. (Byte-exact comparison with `go run` is impossible: Go's stack
// frames carry source positions and `+0x` offsets goclr cannot reproduce, so this asserts
// the Go-shaped framing and exit code.) The fixture lives in a temp module so the repo's
// `go build ./...` is untouched.
package panicfmt

import (
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

const crashProg = `package main

func main() {
	panic("boom-42")
}
`

func TestUncaughtPanicFormat(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping panic-format test (needs dotnet) in -short mode")
	}
	if _, err := exec.LookPath("dotnet"); err != nil {
		t.Skip("dotnet not available")
	}
	root := repoRoot(t)
	rt, st := runtimeDLLs(root)
	if rt == "" || st == "" {
		t.Skip("runtime DLLs not built; set GOCLR_RUNTIME_DLL/GOCLR_STDLIB_DLL or build runtime/")
	}

	goclr := filepath.Join(t.TempDir(), "goclr")
	build := exec.Command("go", "build", "-o", goclr, "github.com/arturoeanton/go-netcore/cmd/goclr")
	build.Dir = root
	if out, err := build.CombinedOutput(); err != nil {
		t.Fatalf("building goclr: %v\n%s", err, out)
	}

	// A throwaway module so goclr loads it standalone.
	dir := t.TempDir()
	mustWrite(t, filepath.Join(dir, "go.mod"), "module crash\n\ngo 1.24\n")
	mustWrite(t, filepath.Join(dir, "main.go"), crashProg)

	cmd := exec.Command(goclr, "run", ".")
	cmd.Dir = dir
	cmd.Env = append(os.Environ(), "GOCLR_RUNTIME_DLL="+rt, "GOCLR_STDLIB_DLL="+st)
	out, err := cmd.CombinedOutput()
	code := 0
	if ee, ok := err.(*exec.ExitError); ok {
		code = ee.ExitCode()
	}
	s := string(out)

	if code != 2 {
		t.Errorf("exit code = %d, want 2\n%s", code, s)
	}
	for _, want := range []string{"panic: boom-42", "goroutine 1 [running]:"} {
		if !strings.Contains(s, want) {
			t.Errorf("output missing %q\n%s", want, s)
		}
	}
	if strings.Contains(s, "Unhandled exception") {
		t.Errorf("got a .NET unhandled-exception dump, want Go-style panic\n%s", s)
	}
}

func runtimeDLLs(root string) (rt, st string) {
	rt = os.Getenv("GOCLR_RUNTIME_DLL")
	st = os.Getenv("GOCLR_STDLIB_DLL")
	if rt == "" {
		p := filepath.Join(root, "runtime", "dotnet", "bin", "Release", "net8.0", "GoCLR.Runtime.dll")
		if _, err := os.Stat(p); err == nil {
			rt = p
		}
	}
	if st == "" {
		for _, p := range []string{
			filepath.Join(root, "GoCLR.Stdlib.dll"),
			filepath.Join(root, "runtime", "stdlib", "bin", "Release", "net8.0", "GoCLR.Stdlib.dll"),
		} {
			if _, err := os.Stat(p); err == nil {
				st = p
				break
			}
		}
	}
	return rt, st
}

func mustWrite(t *testing.T, path, content string) {
	t.Helper()
	if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
		t.Fatal(err)
	}
}

func repoRoot(t *testing.T) string {
	t.Helper()
	abs, err := filepath.Abs("../..")
	if err != nil {
		t.Fatal(err)
	}
	return abs
}
