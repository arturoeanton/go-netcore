package frontend

import (
	"testing"

	"golang.org/x/tools/go/packages"
)

func TestIsStdlibPath(t *testing.T) {
	cases := map[string]bool{
		"fmt":                true,
		"net/http":           true,
		"encoding/json":      true,
		"":                   false,
		"github.com/x/y":     false, // dotted first segment => external module
		"golang.org/x/tools": false,
		"example.com/app":    false,
	}
	for path, want := range cases {
		if got := isStdlibPath(path); got != want {
			t.Errorf("isStdlibPath(%q) = %v, want %v", path, got, want)
		}
	}
}

// TestIsStdlibPkgModule guards the fix where a local module whose path has no dot
// (e.g. "myapp") was misclassified as stdlib by the bare-path heuristic, which made
// the compiler skip its subpackages.
func TestIsStdlibPkgModule(t *testing.T) {
	cases := []struct {
		name    string
		pkgPath string
		module  *packages.Module
		want    bool
	}{
		{"real stdlib has no module", "fmt", nil, true},
		{"dotless main module is not stdlib", "myapp/sub", &packages.Module{Path: "myapp"}, false},
		{"dotless dependency is not stdlib", "tool/pkg", &packages.Module{Path: "tool"}, false},
		{"dotted module is not stdlib", "github.com/x/y/z", &packages.Module{Path: "github.com/x/y"}, false},
		{"std pseudo-module is stdlib", "strings", &packages.Module{Path: "std"}, true},
		{"cmd pseudo-module is stdlib", "internal/foo", &packages.Module{Path: "cmd"}, true},
	}
	for _, c := range cases {
		lp := &packages.Package{PkgPath: c.pkgPath, Module: c.module}
		if got := isStdlibPkg(lp); got != c.want {
			t.Errorf("%s: isStdlibPkg(%q, module=%v) = %v, want %v",
				c.name, c.pkgPath, c.module, got, c.want)
		}
	}
}
