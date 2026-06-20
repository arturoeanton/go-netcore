package cli

import (
	"os"
	"path/filepath"
	"testing"
)

func TestNormalizePatterns(t *testing.T) {
	dir := t.TempDir()
	if err := os.MkdirAll(filepath.Join(dir, "demo"), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(dir, "demo", "main.go"), []byte("package main\nfunc main(){}\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	cwd, _ := os.Getwd()
	defer os.Chdir(cwd)
	if err := os.Chdir(dir); err != nil {
		t.Fatal(err)
	}

	sep := string(filepath.Separator)
	cases := []struct{ in, want string }{
		{"demo", "." + sep + "demo"},                 // bare existing dir -> ./demo
		{"demo/main.go", "." + sep + "demo/main.go"}, // bare existing file -> ./...
		{"./demo", "./demo"},                         // already relative -> unchanged
		{"./...", "./..."},                           // wildcard -> unchanged
		{"fmt", "fmt"},                               // import path (no on-disk match) -> unchanged
		{"github.com/x/y", "github.com/x/y"},         // import path -> unchanged
	}
	for _, c := range cases {
		got := normalizePatterns([]string{c.in})[0]
		if got != c.want {
			t.Errorf("normalizePatterns(%q) = %q, want %q", c.in, got, c.want)
		}
	}
}
