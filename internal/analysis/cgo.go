package analysis

import (
	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
)

// checkCgo rejects any package that imports the pseudo-package "C". cgo is not
// supported on the CLR target (spec §21).
func checkCgo(pkg *frontend.Package, bag *diagnostics.Bag) {
	for _, f := range pkg.Syntax {
		for _, imp := range f.Imports {
			if imp.Path == nil {
				continue
			}
			if imp.Path.Value == `"C"` {
				pos := positionOf(pkg, imp.Pos())
				bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeCgoImport,
					"cgo is not supported on target clr").
					WithPackage(pkg.PkgPath).
					WithPos(pos).
					WithReason("package "+pkg.PkgPath+" imports \"C\"; the CLR backend cannot compile cgo.").
					WithSuggestion("use a pure-Go alternative, add a //go:build goclr implementation, or exclude this dependency for GoCLR builds."))
			}
		}
	}
}
