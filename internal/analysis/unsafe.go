package analysis

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
)

// UnsafeSite records a single use of the unsafe package.
type UnsafeSite struct {
	Pos      diagnostics.Position `json:"position"`
	Selector string               `json:"selector"` // e.g. "unsafe.Sizeof"
	Approved bool                 `json:"approved"`
}

// approvedUnsafe are the unsafe selectors GoCLR tolerates for pure-Go packages
// because they do not depend on exact memory layout in a way the runtime cannot
// emulate (spec §20). Sizeof/Alignof/Offsetof fold to constants; Add/Slice are
// bounded and lowered onto managed slices.
var approvedUnsafe = map[string]bool{
	"unsafe.Sizeof":     true,
	"unsafe.Alignof":    true,
	"unsafe.Offsetof":   true,
	"unsafe.Add":        true,
	"unsafe.Slice":      true,
	"unsafe.SliceData":  true,
	"unsafe.String":     true,
	"unsafe.StringData": true,
}

// checkUnsafe records every unsafe.* use. Approved patterns produce a single
// summarizing warning per package; unapproved patterns (arbitrary pointer
// reinterpretation) are blocking errors.
func checkUnsafe(pkg *frontend.Package, bag *diagnostics.Bag) []UnsafeSite {
	var sites []UnsafeSite
	approvedCount := 0

	for _, f := range pkg.Syntax {
		ast.Inspect(f, func(n ast.Node) bool {
			sel, ok := n.(*ast.SelectorExpr)
			if !ok {
				return true
			}
			ident, ok := sel.X.(*ast.Ident)
			if !ok {
				return true
			}
			// Resolve via type info so we match the real unsafe package and
			// not a local identifier coincidentally named "unsafe".
			obj := pkg.TypesInfo.Uses[ident]
			pkgName, ok := obj.(*types.PkgName)
			if !ok || pkgName.Imported().Path() != "unsafe" {
				return true
			}
			name := "unsafe." + sel.Sel.Name
			site := UnsafeSite{
				Pos:      positionOf(pkg, sel.Pos()),
				Selector: name,
				Approved: approvedUnsafe[name],
			}
			sites = append(sites, site)
			if site.Approved {
				approvedCount++
			} else {
				bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeUnsafePointer,
					"unsupported unsafe operation: "+name).
					WithPackage(pkg.PkgPath).
					WithPos(site.Pos).
					WithReason("arbitrary pointer reinterpretation and pointer arithmetic are not supported by the GoCLR MVP.").
					WithSuggestion("avoid this dependency or add a //go:build goclr replacement that uses safe operations."))
			}
			return true
		})
	}

	if approvedCount > 0 {
		bag.Add(diagnostics.New(diagnostics.SeverityWarn, diagnostics.CodeUnsafeUnknown,
			"unsafe used with approved patterns only").
			WithPackage(pkg.PkgPath).
			WithReason(plural(approvedCount, "approved unsafe site") + " (Sizeof/Alignof/Offsetof/Add/Slice/String)."))
	}
	return sites
}
