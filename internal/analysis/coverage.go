package analysis

import (
	"go/types"
	"sort"

	"github.com/arturoeanton/go-netcore/internal/lower"
	"golang.org/x/tools/go/packages"
)

// SymbolKind classifies an exported stdlib symbol in the coverage matrix.
type SymbolKind int

const (
	SymFunc SymbolKind = iota
	SymMethod
	SymVar
)

func (k SymbolKind) String() string {
	switch k {
	case SymFunc:
		return "func"
	case SymMethod:
		return "method"
	default:
		return "var"
	}
}

// Symbol is one exported func/method/var of a package and whether goclr covers it.
type Symbol struct {
	Name    string     `json:"name"` // "Split", "Reader.Read", "EOF"
	Kind    SymbolKind `json:"-"`
	KindStr string     `json:"kind"`
	Covered bool       `json:"covered"`
}

// PackageCoverage is the per-function coverage of one stdlib package.
type PackageCoverage struct {
	ImportPath string   `json:"importPath"`
	FullSource bool     `json:"fullSource"` // compiled from real Go source (all covered)
	Covered    int      `json:"covered"`
	Total      int      `json:"total"`
	Missing    []string `json:"missing"`           // uncovered symbol names
	Symbols    []Symbol `json:"symbols,omitempty"` // every symbol (verbose only)
}

// Percent is the package's coverage as a 0..100 value (100 when it has no surface).
func (p PackageCoverage) Percent() float64 {
	if p.Total == 0 {
		return 100
	}
	return 100 * float64(p.Covered) / float64(p.Total)
}

// CoverageReport is the whole-standard per-function matrix.
type CoverageReport struct {
	Packages []PackageCoverage `json:"packages"`
	Covered  int               `json:"covered"`
	Total    int               `json:"total"`
}

// Percent is the overall coverage as a 0..100 value.
func (r CoverageReport) Percent() float64 {
	if r.Total == 0 {
		return 100
	}
	return 100 * float64(r.Covered) / float64(r.Total)
}

// ComputeCoverage loads each import path's exported API (funcs, methods on exported
// types, package vars) and cross-references it against goclr's backend coverage,
// producing a per-function matrix. A package compiled wholesale from Go source counts
// as fully covered. keepSymbols retains the full per-symbol list (for verbose output).
func ComputeCoverage(paths []string, keepSymbols bool) (*CoverageReport, error) {
	cd := lower.Coverage()
	cfg := &packages.Config{Mode: packages.NeedName | packages.NeedTypes | packages.NeedImports}
	loaded, err := packages.Load(cfg, paths...)
	if err != nil {
		return nil, err
	}
	rep := &CoverageReport{}
	for _, p := range loaded {
		if p.Types == nil || len(p.Errors) > 0 {
			continue
		}
		pkgPath := p.PkgPath
		pc := PackageCoverage{ImportPath: pkgPath, FullSource: cd.CompiledFromSource[pkgPath]}
		scope := p.Types.Scope()
		for _, name := range scope.Names() {
			obj := scope.Lookup(name)
			if !obj.Exported() {
				continue
			}
			switch o := obj.(type) {
			case *types.Func:
				pc.add(Symbol{Name: name, Kind: SymFunc}, pc.FullSource || cd.Funcs[pkgPath][name])
			case *types.TypeName:
				named, ok := o.Type().(*types.Named)
				if !ok {
					continue
				}
				for i := 0; i < named.NumMethods(); i++ {
					fn := named.Method(i)
					if !fn.Exported() {
						continue
					}
					key := name + "." + fn.Name()
					pc.add(Symbol{Name: key, Kind: SymMethod}, pc.FullSource || cd.Methods[pkgPath][key])
				}
			case *types.Var:
				pc.add(Symbol{Name: name, Kind: SymVar}, pc.FullSource || cd.Vars[pkgPath][name])
			}
		}
		sort.Slice(pc.Symbols, func(i, j int) bool { return pc.Symbols[i].Name < pc.Symbols[j].Name })
		sort.Strings(pc.Missing)
		if !keepSymbols {
			pc.Symbols = nil
		}
		rep.Packages = append(rep.Packages, pc)
		rep.Covered += pc.Covered
		rep.Total += pc.Total
	}
	sort.Slice(rep.Packages, func(i, j int) bool { return rep.Packages[i].ImportPath < rep.Packages[j].ImportPath })
	return rep, nil
}

// add records a symbol's coverage into the package totals.
func (p *PackageCoverage) add(s Symbol, covered bool) {
	s.Covered = covered
	s.KindStr = s.Kind.String()
	p.Symbols = append(p.Symbols, s)
	p.Total++
	if covered {
		p.Covered++
	} else {
		p.Missing = append(p.Missing, s.Kind.String()+" "+s.Name)
	}
}

// DefaultCoveragePackages is the backend's targeted stdlib set — the default scope.
func DefaultCoveragePackages() []string {
	pkgs := lower.TargetedPackages()
	sort.Strings(pkgs)
	return pkgs
}
