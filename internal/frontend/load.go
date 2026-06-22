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
//
// `purego` and `nomsgpack` select dependency-friendly build variants for a target
// goclr cannot lower: `purego` is the widely-honored tag that makes libraries
// (x/sys/cpu, and others) take their no-assembly / no-unsafe path, which is exactly
// what goclr needs since it emits neither; `nomsgpack` drops gin's MessagePack
// binding, removing the ugorji/go/codec dependency (hand-written assembly + heavy
// unsafe). Both are inert for programs that do not consult them.
var BuildTags = []string{"goclr", "clr", "net8", "purego", "nomsgpack", "safe"}

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
	Fset  *token.FileSet
	Roots []*Package
	All   map[string]*Package // keyed by import path; includes transitive deps
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
		Overlay:    StdlibOverlay(env, cfg.Tests),
	}

	loaded, err := packages.Load(pcfg, cfg.Patterns...)
	if err != nil {
		return nil, fmt.Errorf("loading packages: %w", err)
	}

	res := &Result{All: map[string]*Package{}}
	var fset *token.FileSet

	// Guard recursion by package ID, not import path: with Tests:true a package and its
	// test variant share one import path ("foo" and "foo [foo.test]") but are distinct
	// packages, and the generated test main imports the test variant — deduping by path
	// would resolve it to the plain package and lose the TestXxx functions.
	seenByID := map[string]*Package{}
	var convert func(lp *packages.Package) *Package
	convert = func(lp *packages.Package) *Package {
		if existing, ok := seenByID[lp.ID]; ok {
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
		seenByID[lp.ID] = p
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
