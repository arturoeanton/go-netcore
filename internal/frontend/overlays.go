package frontend

import (
	"embed"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

// overlayFS holds the goclr-provided source overlays for standard-library packages
// that goclr compiles from a safe reimplementation instead of their real source
// (e.g. to drop dependencies it cannot lower). These are part of goclr's stdlib
// support and apply to every program; they are NOT application-specific.
//
//go:embed overlays/sort/sort.go.txt
//go:embed overlays/sort/search.go.txt
//go:embed overlays/slices/slices.go.txt
//go:embed overlays/slices/iter.go.txt
//go:embed overlays/httptrace/trace.go.txt
//go:embed overlays/iter/iter.go.txt
//go:embed overlays/maps/maps.go.txt
var overlayFS embed.FS

// projectOverlayDir is the convention directory, relative to a project's module
// root, where the project supplies goclr-safe replacements for its own vendored
// dependency files (e.g. third-party code that uses unsafe.Pointer). The compiler
// is agnostic to which dependencies these are: it applies whatever it finds, so a
// program is responsible for its own overlays. Layout mirrors import paths:
//
//	goclr.overlays/<import/path>/<file>.go.txt  ->  vendor/<import/path>/<file>.go
const projectOverlayDir = "goclr.overlays"

// stdlibOverlayPkg describes a standard-library package that goclr compiles from
// a goclr-provided source overlay instead of the real GOROOT source (e.g. to drop
// a dependency goclr cannot lower, like internal/reflectlite). Files named in
// `files` are replaced with the embedded content; every other .go file in the
// package directory is blanked to an empty `package <name>` to avoid conflicting
// or unsupported declarations.
type stdlibOverlayPkg struct {
	importPath string
	files      map[string]string // base filename -> embed path
	// replaceOnly keeps the package's other .go files as their real GOROOT source
	// (only the named files are swapped). Use when just one file is unlowerable and
	// the rest compile fine — blanking them would drop declarations the patched file
	// still depends on.
	replaceOnly bool
}

var stdlibOverlayPkgs = []stdlibOverlayPkg{
	{importPath: "sort", files: map[string]string{
		"sort.go":   "overlays/sort/sort.go.txt",
		"search.go": "overlays/sort/search.go.txt",
	}},
	// slices: only slices.go uses unsafe (the overlaps/startIdx aliasing checks).
	// Replace just that file; iter.go/sort.go/zsort*.go compile from real source.
	{importPath: "slices", replaceOnly: true, files: map[string]string{
		"slices.go": "overlays/slices/slices.go.txt",
		"iter.go":   "overlays/slices/iter.go.txt",
	}},
	// net/http/httptrace: the real package pulls crypto/tls + internal/nettrace and
	// uses reflect. The overlay keeps only the client-trace surface x/net/http2 reads
	// (dead weight for a server) so it compiles without those deps.
	{importPath: "net/http/httptrace", files: map[string]string{
		"trace.go": "overlays/httptrace/trace.go.txt",
	}},
	// iter: the real package's Pull/Pull2 use runtime coroutine intrinsics
	// (newcoro/coroswitch, bodyless linkname). Keep only the Seq/Seq2 types that
	// range-over-func and the maps/slices helpers need.
	{importPath: "iter", files: map[string]string{
		"iter.go": "overlays/iter/iter.go.txt",
	}},
	// maps: maps.go linknames a runtime clone (bodyless). Replace just that file with
	// a pure-Go Clone; the iterator helpers in maps/iter.go compile from real source.
	{importPath: "maps", replaceOnly: true, files: map[string]string{
		"maps.go": "overlays/maps/maps.go.txt",
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
			if p.replaceOnly {
				continue // leave this file as its real GOROOT source
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

// ApplyOverlays replaces vendored dependency files with the project-supplied
// goclr-safe versions found under <moduleRoot>/goclr.overlays (see
// projectOverlayDir). Each `<path>.go.txt` overlay overwrites `vendor/<path>.go`.
// It is a no-op when the project has no overlay directory or is not vendored, and
// never touches files outside vendor/. Returns the count applied.
//
// The compiler holds no knowledge of which dependencies need overlays — that is
// the project's concern — so goclr stays agnostic to any particular application.
func ApplyOverlays(dir string, env []string) int {
	root := moduleRoot(dir, env)
	if root == "" {
		return 0
	}
	overlayRoot := filepath.Join(root, projectOverlayDir)
	vendor := filepath.Join(root, "vendor")
	if !isDir(overlayRoot) || !isDir(vendor) {
		return 0
	}
	n := 0
	_ = filepath.WalkDir(overlayRoot, func(path string, d os.DirEntry, err error) error {
		if err != nil || d.IsDir() || !strings.HasSuffix(path, ".go.txt") {
			return nil
		}
		rel, relErr := filepath.Rel(overlayRoot, path)
		if relErr != nil {
			return nil
		}
		target := filepath.Join(vendor, strings.TrimSuffix(rel, ".txt"))
		if _, statErr := os.Stat(target); statErr != nil {
			return nil // dependency not vendored
		}
		content, readErr := os.ReadFile(path)
		if readErr != nil {
			return nil
		}
		if os.WriteFile(target, content, 0o644) == nil {
			n++
		}
		return nil
	})
	return n
}

func isDir(p string) bool {
	st, err := os.Stat(p)
	return err == nil && st.IsDir()
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
