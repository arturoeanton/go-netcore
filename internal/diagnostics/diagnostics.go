// Package diagnostics defines the GoCLR diagnostic model: stable error codes
// (GCLRxxxx), severities, source positions, and actionable messages.
//
// Every user-facing problem produced by goclr should flow through a Diagnostic
// so that output is consistent across the CLI commands and can be rendered both
// for humans and as JSON.
package diagnostics

import (
	"fmt"
	"sort"
)

// Severity describes how a Diagnostic affects the build/analysis outcome.
type Severity int

const (
	// SeverityInfo is informational and never blocks.
	SeverityInfo Severity = iota
	// SeverityWarn indicates a supported-but-degraded situation (e.g. partial
	// reflect). It does not block by itself.
	SeverityWarn
	// SeverityError is blocking: the package cannot be compiled for the CLR
	// target under the active profile.
	SeverityError
)

func (s Severity) String() string {
	switch s {
	case SeverityInfo:
		return "info"
	case SeverityWarn:
		return "warn"
	case SeverityError:
		return "error"
	default:
		return "unknown"
	}
}

// Code is a stable GoCLR diagnostic code. Categories follow the spec:
//
//	GCLR01xx loading / build / cgo
//	GCLR02xx unsafe / asm
//	GCLR03xx unsupported language / runtime feature
//	GCLR04xx stdlib missing API
//	GCLR05xx CLR emission error
//	GCLR06xx linker / package init error
//	GCLR07xx runtime failure
type Code string

const (
	// GCLR01xx loading / build / cgo
	CodeCgoImport     Code = "GCLR0100"
	CodeCgoRequired   Code = "GCLR0101"
	CodeLoadFailure   Code = "GCLR0102"
	CodeBuildConstr   Code = "GCLR0103"
	CodeNoMainPackage Code = "GCLR0104"

	// GCLR02xx unsafe / asm
	CodeAsmFile       Code = "GCLR0200"
	CodeUnsafePointer Code = "GCLR0201"
	CodeUnsafeUnknown Code = "GCLR0202"

	// GCLR03xx unsupported language / runtime feature
	CodeUnsupportedFeature Code = "GCLR0301"
	CodeUnsupportedReflect Code = "GCLR0302"

	// GCLR04xx stdlib missing API
	CodeStdlibMissing Code = "GCLR0401"

	// GCLR05xx CLR emission error
	CodeEmitFailure Code = "GCLR0500"

	// GCLR06xx linker / package init error
	CodeLinkFailure Code = "GCLR0600"
	CodeInitCycle   Code = "GCLR0601"

	// GCLR07xx runtime failure
	CodeRuntimeFailure Code = "GCLR0700"
)

// Position is a source location. Line/Col are 1-based; zero means "unknown".
type Position struct {
	File string `json:"file"`
	Line int    `json:"line"`
	Col  int    `json:"col"`
}

func (p Position) String() string {
	if p.File == "" {
		return ""
	}
	if p.Line == 0 {
		return p.File
	}
	if p.Col == 0 {
		return fmt.Sprintf("%s:%d", p.File, p.Line)
	}
	return fmt.Sprintf("%s:%d:%d", p.File, p.Line, p.Col)
}

// Diagnostic is a single actionable message.
type Diagnostic struct {
	Code       Code     `json:"code"`
	Severity   Severity `json:"-"`
	SeverityID string   `json:"severity"`
	Package    string   `json:"package,omitempty"`
	Pos        Position `json:"position,omitempty"`
	Message    string   `json:"message"`
	Reason     string   `json:"reason,omitempty"`
	Suggestion string   `json:"suggestion,omitempty"`
}

// New constructs a Diagnostic and fills SeverityID for JSON output.
func New(sev Severity, code Code, msg string) *Diagnostic {
	return &Diagnostic{
		Code:       code,
		Severity:   sev,
		SeverityID: sev.String(),
		Message:    msg,
	}
}

// WithPackage sets the owning import path (fluent).
func (d *Diagnostic) WithPackage(pkg string) *Diagnostic { d.Package = pkg; return d }

// WithPos sets the source position (fluent).
func (d *Diagnostic) WithPos(p Position) *Diagnostic { d.Pos = p; return d }

// WithReason sets the explanation (fluent).
func (d *Diagnostic) WithReason(r string) *Diagnostic { d.Reason = r; return d }

// WithSuggestion sets a remediation hint (fluent).
func (d *Diagnostic) WithSuggestion(s string) *Diagnostic { d.Suggestion = s; return d }

// Bag accumulates diagnostics during a compilation/analysis run.
type Bag struct {
	items []*Diagnostic
}

// Add appends a diagnostic.
func (b *Bag) Add(d *Diagnostic) { b.items = append(b.items, d) }

// Addf is a convenience constructor + add.
func (b *Bag) Addf(sev Severity, code Code, format string, args ...any) *Diagnostic {
	d := New(sev, code, fmt.Sprintf(format, args...))
	b.Add(d)
	return d
}

// Items returns the accumulated diagnostics.
func (b *Bag) Items() []*Diagnostic { return b.items }

// HasErrors reports whether any blocking diagnostic was recorded.
func (b *Bag) HasErrors() bool {
	for _, d := range b.items {
		if d.Severity == SeverityError {
			return true
		}
	}
	return false
}

// Counts returns the number of diagnostics per severity.
func (b *Bag) Counts() (info, warn, errs int) {
	for _, d := range b.items {
		switch d.Severity {
		case SeverityInfo:
			info++
		case SeverityWarn:
			warn++
		case SeverityError:
			errs++
		}
	}
	return
}

// Sorted returns the diagnostics ordered by severity (errors first), then
// package, then position. The returned slice is a copy.
func (b *Bag) Sorted() []*Diagnostic {
	out := make([]*Diagnostic, len(b.items))
	copy(out, b.items)
	sort.SliceStable(out, func(i, j int) bool {
		if out[i].Severity != out[j].Severity {
			return out[i].Severity > out[j].Severity
		}
		if out[i].Package != out[j].Package {
			return out[i].Package < out[j].Package
		}
		if out[i].Pos.File != out[j].Pos.File {
			return out[i].Pos.File < out[j].Pos.File
		}
		return out[i].Pos.Line < out[j].Pos.Line
	})
	return out
}
