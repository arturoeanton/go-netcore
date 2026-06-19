package frontend

import (
	"os"
	"strings"
)

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
