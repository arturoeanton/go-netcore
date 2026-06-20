package frontend

import (
	"os"
	"strings"

	"golang.org/x/tools/go/packages"
)

// isStdlibPkg reports whether a loaded package is part of the Go standard library
// (and so must be shimmed/overlaid rather than lowered from source). A package that
// belongs to a real module — the main module or any dependency — is never stdlib,
// even when its module path has no dot (e.g. a local module named "myapp" with a
// subpackage "myapp/sub"). The bare-path heuristic alone misclassifies those.
func isStdlibPkg(lp *packages.Package) bool {
	if m := lp.Module; m != nil && m.Path != "" && m.Path != "std" && m.Path != "cmd" {
		return false
	}
	return isStdlibPath(lp.PkgPath)
}

// envBase returns the current process environment as a slice, used as the base
// for package loads. Callers append GoCLR-specific overrides (e.g. CGO_ENABLED).
func envBase() []string {
	return os.Environ()
}

// stdlibFirstSegments is a fast pre-filter: import paths whose first path
// segment contains a dot are module paths (e.g. github.com/...), never stdlib.
func isStdlibPath(importPath string) bool {
	if importPath == "" {
		return false
	}
	first := importPath
	if i := strings.IndexByte(importPath, '/'); i >= 0 {
		first = importPath[:i]
	}
	// A dot in the first segment means a domain => external module.
	return !strings.Contains(first, ".")
}
