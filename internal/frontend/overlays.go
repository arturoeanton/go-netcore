package frontend

import (
	"embed"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

// overlayFS holds goclr-safe replacements for the handful of third-party files
// that use unsafe.Pointer (byte<->numeric reinterpretation). When the project is
// vendored, ApplyOverlays overwrites the vendored copy with the safe version so
// the backend never sees the unsafe code. The reinterpret patterns map to the
// encoding/binary shim; see GOJA-STRATEGY.md.
//
//go:embed overlays/regexp2helpers/indexof.go.txt
//go:embed overlays/goja/typedarrays.go.txt
//go:embed overlays/goja/builtin_typedarrays.go.txt
//go:embed overlays/unistring/string.go.txt
//go:embed overlays/gojaval/value.go.txt
var overlayFS embed.FS

type overlayFile struct {
	module  string // go module path
	relpath string // file path within the module
	embed   string // path within overlayFS
}

// overlayFiles registers each dep file that goclr replaces with a safe version.
var overlayFiles = []overlayFile{
	{"github.com/dlclark/regexp2/v2", "helpers/indexof.go", "overlays/regexp2helpers/indexof.go.txt"},
	{"github.com/dop251/goja", "typedarrays.go", "overlays/goja/typedarrays.go.txt"},
	{"github.com/dop251/goja", "builtin_typedarrays.go", "overlays/goja/builtin_typedarrays.go.txt"},
	{"github.com/dop251/goja/unistring", "string.go", "overlays/unistring/string.go.txt"},
	{"github.com/dop251/goja", "value.go", "overlays/gojaval/value.go.txt"},
}

// ApplyOverlays, when the module is vendored, replaces each registered unsafe dep
// file in vendor/ with its goclr-safe version. It is a no-op (and harmless) when
// there is no vendor directory. Returns the count applied.
func ApplyOverlays(dir string, env []string) int {
	root := moduleRoot(dir, env)
	if root == "" {
		return 0
	}
	vendor := filepath.Join(root, "vendor")
	if st, err := os.Stat(vendor); err != nil || !st.IsDir() {
		return 0
	}
	n := 0
	for _, of := range overlayFiles {
		content, err := overlayFS.ReadFile(of.embed)
		if err != nil {
			continue
		}
		target := filepath.Join(vendor, filepath.FromSlash(of.module), filepath.FromSlash(of.relpath))
		if _, err := os.Stat(target); err != nil {
			continue // dep not vendored
		}
		if err := os.WriteFile(target, content, 0o644); err == nil {
			n++
		}
	}
	return n
}

func moduleRoot(dir string, env []string) string {
	cmd := exec.Command("go", "env", "GOMOD")
	cmd.Dir = dir
	cmd.Env = env
	b, err := cmd.Output()
	if err != nil {
		return ""
	}
	gomod := strings.TrimSpace(string(b))
	if gomod == "" || gomod == "/dev/null" {
		return ""
	}
	return filepath.Dir(gomod)
}
