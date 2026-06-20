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
//go:embed overlays/sort/sort.go.txt
//go:embed overlays/sort/search.go.txt
var overlayFS embed.FS

// stdlibOverlayPkg describes a standard-library package that goclr compiles from
// a goclr-provided source overlay instead of the real GOROOT source (e.g. to drop
// a dependency goclr cannot lower, like internal/reflectlite). Files named in
// `files` are replaced with the embedded content; every other .go file in the
// package directory is blanked to an empty `package <name>` to avoid conflicting
// or unsupported declarations.
type stdlibOverlayPkg struct {
	importPath string
	files      map[string]string // base filename -> embed path
}

var stdlibOverlayPkgs = []stdlibOverlayPkg{
	{"sort", map[string]string{
		"sort.go":   "overlays/sort/sort.go.txt",
		"search.go": "overlays/sort/search.go.txt",
	}},
}

// StdlibOverlay builds the go/packages Overlay map (absolute file path -> content)
// that virtually replaces the source of overlaid stdlib packages. It is safe to
// pass to packages.Config.Overlay even when empty.
func StdlibOverlay(env []string) map[string][]byte {
	goroot := goEnv("GOROOT", env)
	if goroot == "" {
		return nil
	}
	out := map[string][]byte{}
	for _, p := range stdlibOverlayPkgs {
		dir := filepath.Join(goroot, "src", filepath.FromSlash(p.importPath))
		entries, err := os.ReadDir(dir)
		if err != nil {
			continue
		}
		name := filepath.Base(p.importPath)
		for _, e := range entries {
			if e.IsDir() || !strings.HasSuffix(e.Name(), ".go") || strings.HasSuffix(e.Name(), "_test.go") {
				continue
			}
			full := filepath.Join(dir, e.Name())
			if embedPath, ok := p.files[e.Name()]; ok {
				if content, err := overlayFS.ReadFile(embedPath); err == nil {
					out[full] = content
				}
				continue
			}
			// Blank every other file so its declarations don't conflict.
			out[full] = []byte("package " + name + "\n")
		}
	}
	return out
}

func goEnv(key string, env []string) string {
	cmd := exec.Command("go", "env", key)
	cmd.Env = env
	b, err := cmd.Output()
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(b))
}

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
