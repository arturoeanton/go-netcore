// Package analysis implements compatibility analysis for the GoCLR target: it
// rejects cgo/assembly, classifies unsafe usage, and reports how each imported
// package maps onto the GoCLR stdlib overlay for a given profile.
package analysis

import (
	"fmt"
	"go/token"
	"sort"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
)

// Profile names a compatibility profile.
type Profile string

const (
	// ProfileEchoGoja is the MVP profile targeting Echo v4 + goja.
	ProfileEchoGoja Profile = "echo-goja"
)

// PackageVerdict is the per-package compatibility outcome.
type PackageVerdict struct {
	ImportPath string       `json:"import_path"`
	Kind       string       `json:"kind"`   // "main", "module", "stdlib"
	Status     string       `json:"status"` // "OK", "WARN", "FAIL"
	Overlay    string       `json:"overlay,omitempty"`
	Notes      []string     `json:"notes,omitempty"`
	Unsafe     []UnsafeSite `json:"unsafe,omitempty"`
}

// Report is the full compatibility analysis result.
type Report struct {
	Profile     Profile                   `json:"profile"`
	Summary     ReportSummary             `json:"summary"`
	Packages    []PackageVerdict          `json:"packages"`
	Diagnostics []*diagnostics.Diagnostic `json:"diagnostics"`
	Compatible  bool                      `json:"compatible"`
	bag         *diagnostics.Bag
}

// ReportSummary is the stable, machine-readable headline of a report: per-status package
// counts and the stdlib overlay coverage. Consumers can track these over time.
type ReportSummary struct {
	Packages int            `json:"packages"`
	OK       int            `json:"ok"`
	Warn     int            `json:"warn"`
	Fail     int            `json:"fail"`
	Stdlib   StdlibCoverage `json:"stdlib"`
}

// Analyze runs the full compatibility analysis over a loaded result.
func Analyze(res *frontend.Result, profile Profile) *Report {
	bag := &diagnostics.Bag{}
	rep := &Report{Profile: profile, bag: bag}

	// Stable iteration over all packages (roots + transitive deps).
	paths := make([]string, 0, len(res.All))
	for p := range res.All {
		paths = append(paths, p)
	}
	sort.Strings(paths)

	rootSet := map[string]bool{}
	for _, r := range res.Roots {
		rootSet[r.PkgPath] = true
	}

	for _, path := range paths {
		pkg := res.All[path]
		rep.Packages = append(rep.Packages, analyzePackage(pkg, rootSet[path], bag))
	}

	rep.Diagnostics = bag.Sorted()
	rep.Compatible = !bag.HasErrors()
	rep.Summary = rep.computeSummary()
	return rep
}

// computeSummary tallies per-status package counts and stdlib coverage.
func (r *Report) computeSummary() ReportSummary {
	s := ReportSummary{Packages: len(r.Packages), Stdlib: r.StdlibSummary()}
	for _, p := range r.Packages {
		switch p.Status {
		case "OK":
			s.OK++
		case "WARN":
			s.Warn++
		case "FAIL":
			s.Fail++
		}
	}
	return s
}

func analyzePackage(pkg *frontend.Package, isRoot bool, bag *diagnostics.Bag) PackageVerdict {
	v := PackageVerdict{ImportPath: pkg.PkgPath}
	before := len(bag.Items())

	// Surface loader errors first (e.g. type-check failures).
	for _, e := range pkg.Errors {
		bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeLoadFailure, e.Msg).
			WithPackage(pkg.PkgPath).
			WithPos(parseGoPos(e.Pos)))
	}

	switch {
	case pkg.Name == "main":
		v.Kind = "main"
	case pkg.IsStdlib:
		v.Kind = "stdlib"
	default:
		v.Kind = "module"
	}

	if pkg.IsStdlib {
		// Stdlib coverage is GoCLR's own responsibility via the overlay, not a
		// defect in the user's program. We never hard-FAIL on a stdlib package:
		// missing overlay coverage is reported as a pending runtime requirement.
		// Assembly inside stdlib is expected (math, crypto, runtime) and is
		// replaced by managed code in the overlay, so checkAsm is not applied.
		checkCgo(pkg, bag) // pure stdlib never imports "C"; cheap sanity check.
		analyzeStdlibOverlay(pkg, &v, bag)
	} else {
		// User code and third-party modules: real blockers live here.
		checkCgo(pkg, bag)
		checkAsm(pkg, bag)
		v.Unsafe = checkUnsafe(pkg, bag)
	}

	// Derive the per-package status from the diagnostics it produced.
	v.Status = "OK"
	for _, d := range bag.Items()[before:] {
		switch d.Severity {
		case diagnostics.SeverityError:
			v.Status = "FAIL"
		case diagnostics.SeverityWarn:
			if v.Status != "FAIL" {
				v.Status = "WARN"
			}
		}
	}
	return v
}

// analyzeStdlibOverlay records how a stdlib package maps onto the GoCLR overlay.
func analyzeStdlibOverlay(pkg *frontend.Package, v *PackageVerdict, bag *diagnostics.Bag) {
	status, ok := LookupOverlay(pkg.PkgPath)
	switch {
	case ok && status == OverlayFull:
		v.Overlay = "full"
		v.Notes = append(v.Notes, "provided by GoCLR stdlib overlay")
	case ok && status == OverlayPartial:
		v.Overlay = "partial"
		v.Notes = append(v.Notes, "partial overlay (supported subset only)")
		bag.Add(diagnostics.New(diagnostics.SeverityWarn, diagnostics.CodeStdlibMissing,
			pkg.PkgPath+": supported subset only").
			WithPackage(pkg.PkgPath).
			WithReason("the GoCLR overlay implements a documented subset of " + pkg.PkgPath + "."))
	default:
		// Not yet curated. Pending overlay work for goclr — a warning, not a
		// blocking error, because it does not indicate a problem in user code.
		v.Overlay = "pending"
		v.Notes = append(v.Notes, "overlay pending")
		bag.Add(diagnostics.New(diagnostics.SeverityWarn, diagnostics.CodeStdlibMissing,
			pkg.PkgPath+": overlay not yet provided").
			WithPackage(pkg.PkgPath).
			WithReason("the echo-goja profile does not yet include an overlay for " + pkg.PkgPath + ".").
			WithSuggestion("track this under stdlib/; it is a goclr work item, not a defect in your program."))
	}
}

// RenderText renders the human-readable compatibility report (spec §33). Stdlib
// packages are collapsed into a coverage summary to keep the focus on the user's
// own code and third-party modules.
func (r *Report) RenderText(sb *strings.Builder) { r.render(sb, false) }

// RenderTextVerbose renders every package, including each stdlib import.
func (r *Report) RenderTextVerbose(sb *strings.Builder) { r.render(sb, true) }

func (r *Report) render(sb *strings.Builder, verbose bool) {
	fmt.Fprintf(sb, "Package compatibility report\n\n")
	fmt.Fprintf(sb, "Compatibility profile: %s\n\n", r.Profile)

	for _, p := range r.Packages {
		if p.Kind == "stdlib" && !verbose {
			continue
		}
		note := ""
		if len(p.Notes) > 0 {
			note = strings.Join(p.Notes, "; ")
		}
		fmt.Fprintf(sb, "%-6s %-40s %s\n", p.Status, p.ImportPath, note)
	}
	fmt.Fprintln(sb)

	cov := r.StdlibSummary()
	if total := cov.Full + cov.Partial + cov.Pending; total > 0 {
		fmt.Fprintf(sb, "stdlib overlay: %d required — %d full, %d partial, %d pending\n\n",
			total, cov.Full, cov.Partial, cov.Pending)
	}

	// Collect diagnostics worth showing. By default the per-package "overlay
	// pending" warnings are folded into the summary above, so we hide them here
	// unless verbose; errors and other warnings always show.
	var shown []*diagnostics.Diagnostic
	hiddenStdlib := 0
	for _, d := range r.Diagnostics {
		if !verbose && d.Severity == diagnostics.SeverityWarn && d.Code == diagnostics.CodeStdlibMissing {
			hiddenStdlib++
			continue
		}
		shown = append(shown, d)
	}
	if len(shown) > 0 {
		fmt.Fprintln(sb, "Diagnostics:")
		for _, d := range shown {
			loc := d.Pos.String()
			if loc != "" {
				loc = " (" + loc + ")"
			}
			fmt.Fprintf(sb, "  %-5s %s: %s%s\n", strings.ToUpper(d.Severity.String()), d.Code, d.Message, loc)
		}
		fmt.Fprintln(sb)
	}
	if hiddenStdlib > 0 {
		fmt.Fprintf(sb, "(%d stdlib overlay notes hidden; use --verbose to show)\n\n", hiddenStdlib)
	}

	if r.Compatible {
		fmt.Fprintf(sb, "Result: compatible with profile %s\n", r.Profile)
	} else {
		fmt.Fprintf(sb, "Result: NOT compatible with profile %s\n", r.Profile)
	}
}

// StdlibCoverage summarizes overlay coverage across stdlib imports.
type StdlibCoverage struct {
	Full    int `json:"full"`
	Partial int `json:"partial"`
	Pending int `json:"pending"`
}

// StdlibSummary computes overlay coverage across all analyzed stdlib packages.
func (r *Report) StdlibSummary() StdlibCoverage {
	var c StdlibCoverage
	for _, p := range r.Packages {
		if p.Kind != "stdlib" {
			continue
		}
		switch p.Overlay {
		case "full":
			c.Full++
		case "partial":
			c.Partial++
		default:
			c.Pending++
		}
	}
	return c
}

// RenderDiagnostics writes just the diagnostics (no package table), using the
// full per-diagnostic format. Used by build failures.
func (r *Report) RenderDiagnostics(sb *strings.Builder) {
	for i, d := range r.Diagnostics {
		if i > 0 {
			sb.WriteString("\n")
		}
		d.RenderText(sb)
	}
}

// positionOf converts a token.Pos within a package to a diagnostics.Position.
func positionOf(pkg *frontend.Package, pos token.Pos) diagnostics.Position {
	if pkg.Fset == nil || pos == token.NoPos {
		return diagnostics.Position{}
	}
	p := pkg.Fset.Position(pos)
	return diagnostics.Position{File: p.Filename, Line: p.Line, Col: p.Column}
}

// parseGoPos parses a "file:line:col" string as produced by packages.Error.
func parseGoPos(s string) diagnostics.Position {
	if s == "" {
		return diagnostics.Position{}
	}
	// Split from the right to tolerate Windows drive letters.
	parts := strings.Split(s, ":")
	pos := diagnostics.Position{File: s}
	if len(parts) >= 3 {
		pos.File = strings.Join(parts[:len(parts)-2], ":")
		fmt.Sscanf(parts[len(parts)-2], "%d", &pos.Line)
		fmt.Sscanf(parts[len(parts)-1], "%d", &pos.Col)
	}
	return pos
}

func plural(n int, noun string) string {
	if n == 1 {
		return fmt.Sprintf("%d %s", n, noun)
	}
	return fmt.Sprintf("%d %ss", n, noun)
}
