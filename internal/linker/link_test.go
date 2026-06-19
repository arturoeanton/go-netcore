package linker

import (
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

func TestLinkProducesBundle(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping linker bundle test (may build the runtime) in -short mode")
	}
	if _, err := exec.LookPath("dotnet"); err != nil {
		t.Skip("dotnet not available")
	}

	dir := t.TempDir()
	dll := filepath.Join(dir, "app.dll")
	if err := os.WriteFile(dll, []byte("not a real dll"), 0o644); err != nil {
		t.Fatal(err)
	}

	if err := Link(dll); err != nil {
		t.Fatalf("Link: %v", err)
	}

	// runtimeconfig.json present and rolls forward.
	cfg, err := os.ReadFile(filepath.Join(dir, "app.runtimeconfig.json"))
	if err != nil {
		t.Fatalf("runtimeconfig.json missing: %v", err)
	}
	if !strings.Contains(string(cfg), "LatestMajor") {
		t.Errorf("runtimeconfig.json should roll forward: %s", cfg)
	}
	if !strings.Contains(string(cfg), "Microsoft.NETCore.App") {
		t.Errorf("runtimeconfig.json missing framework: %s", cfg)
	}

	// Runtime assembly copied next to the app.
	rt := filepath.Join(dir, "GoCLR.Runtime.dll")
	info, err := os.Stat(rt)
	if err != nil {
		t.Fatalf("GoCLR.Runtime.dll not copied: %v", err)
	}
	if info.Size() == 0 {
		t.Error("copied GoCLR.Runtime.dll is empty")
	}
}
