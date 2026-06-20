// Package frontend loads Go packages and their type information using the
// public go/packages tooling. It is intentionally decoupled from the GoCLR
// backend so that, in a future phase, the backend could be re-targeted at
// cmd/compile's internal SSA without rewriting the loader.
package frontend

import (
	"fmt"
	"go/ast"
	"go/token"
	"go/types"

	"golang.org/x/tools/go/packages"
)

// BuildTags are the build constraints goclr defines for every load. They mirror
// the spec: code may guard CLR-specific implementations behind `goclr`/`clr`/
// `net8`, and cgo is always disabled.
var BuildTags = []string{"goclr", "clr", "net8"}

// LoadConfig controls a package load.
type LoadConfig struct {
	// Dir is the working directory the patterns are resolved against.
	Dir string
	// Patterns are the package patterns (e.g. "./cmd/server", "./...").
	Patterns []string
	// ExtraTags are additional build tags appended to the GoCLR defaults.
	ExtraTags []string
	// Tests includes test files in the loaded packages.
	Tests bool
}

// Package is a loaded Go package with the information the backend needs. It is a
// thin, stable view over packages.Package so the rest of goclr does not depend
// on go/packages internals directly.
type Package struct {
	PkgPath    string
	Name       string
	GoFiles    []string
	OtherFiles []string // non-Go files: .s, .c, .h, etc.
	Syntax     []*ast.File
	Fset       *token.FileSet
	Types      *types.Package
	TypesInfo  *types.Info
	Imports    map[string]*Package
	Errors     []packages.Error
	// IsStdlib reports whether this is a standard-library package.
	IsStdlib bool
}

// Result is the outcome of a load.
type Result struct {
	Fset     *token.FileSet
	Roots    []*Package
	All      map[string]*Package // keyed by import path; includes transitive deps
}

// Load loads the requested patterns with full type information.
func Load(cfg LoadConfig) (*Result, error) {
	tags := append([]string{}, BuildTags...)
	tags = append(tags, cfg.ExtraTags...)

	mode := packages.NeedName |
		packages.NeedFiles |
		packages.NeedCompiledGoFiles |
		packages.NeedImports |
		packages.NeedDeps |
		packages.NeedTypes |
		packages.NeedSyntax |
		packages.NeedTypesInfo |
		packages.NeedModule

	env := append(envBase(), "CGO_ENABLED=0")
	// Replace any vendored unsafe.Pointer dep files with goclr-safe versions.
	ApplyOverlays(cfg.Dir, env)
	pcfg := &packages.Config{
		Mode:       mode,
		Dir:        cfg.Dir,
		Tests:      cfg.Tests,
		BuildFlags: []string{"-tags=" + joinTags(tags)},
		Env:        env,
		Overlay:    StdlibOverlay(env),
	}

	loaded, err := packages.Load(pcfg, cfg.Patterns...)
	if err != nil {
		return nil, fmt.Errorf("loading packages: %w", err)
	}

	res := &Result{All: map[string]*Package{}}
	var fset *token.FileSet

	var convert func(lp *packages.Package) *Package
	convert = func(lp *packages.Package) *Package {
		if existing, ok := res.All[lp.PkgPath]; ok {
			return existing
		}
		p := &Package{
			PkgPath:    lp.PkgPath,
			Name:       lp.Name,
			GoFiles:    lp.GoFiles,
			OtherFiles: lp.OtherFiles,
			Syntax:     lp.Syntax,
			Fset:       lp.Fset,
			Types:      lp.Types,
			TypesInfo:  lp.TypesInfo,
			Imports:    map[string]*Package{},
			Errors:     lp.Errors,
			IsStdlib:   isStdlibPkg(lp),
		}
		res.All[lp.PkgPath] = p
		if lp.Fset != nil && fset == nil {
			fset = lp.Fset
		}
		for ip, dep := range lp.Imports {
			p.Imports[ip] = convert(dep)
		}
		return p
	}

	for _, lp := range loaded {
		res.Roots = append(res.Roots, convert(lp))
	}
	res.Fset = fset
	return res, nil
}

func joinTags(tags []string) string {
	out := ""
	for i, t := range tags {
		if i > 0 {
			out += ","
		}
		out += t
	}
	return out
}
