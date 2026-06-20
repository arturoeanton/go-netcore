package analysis

import (
	"path/filepath"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
)

// checkAsm rejects Go assembly (.s) files. The CLR backend has no way to lower
// architecture-specific assembly (spec §39, "no .s files").
func checkAsm(pkg *frontend.Package, bag *diagnostics.Bag) {
	for _, f := range pkg.OtherFiles {
		ext := strings.ToLower(filepath.Ext(f))
		switch ext {
		case ".s":
			bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeAsmFile,
				"Go assembly is not supported on target clr").
				WithPackage(pkg.PkgPath).
				WithPos(diagnostics.Position{File: f}).
				WithReason("file " + filepath.Base(f) + " is architecture-specific assembly; the CLR backend cannot lower it.").
				WithSuggestion("use a pure-Go path guarded by //go:build goclr, or exclude this dependency for GoCLR builds."))
		case ".c", ".h", ".cpp", ".cc":
			bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeCgoRequired,
				"native C/C++ source is not supported on target clr").
				WithPackage(pkg.PkgPath).
				WithPos(diagnostics.Position{File: f}).
				WithReason("file " + filepath.Base(f) + " requires a native toolchain (cgo); unsupported on the CLR target.").
				WithSuggestion("use a pure-Go alternative or exclude this dependency for GoCLR builds."))
		}
	}
}
