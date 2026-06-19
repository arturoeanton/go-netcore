package diagnostics

import (
	"fmt"
	"io"
	"strings"
)

// RenderText writes a human-readable rendering of a single diagnostic, in the
// spec's format:
//
//	error GCLR0302: unsupported feature: reflect.MakeFunc
//	package: github.com/foo/bar
//	file: vendor/foo/bar.go:128:12
//
//	Reason:
//	  ...
//
//	Suggestion:
//	  ...
func (d *Diagnostic) RenderText(w io.Writer) {
	fmt.Fprintf(w, "%s %s: %s\n", d.Severity, d.Code, d.Message)
	if d.Package != "" {
		fmt.Fprintf(w, "package: %s\n", d.Package)
	}
	if pos := d.Pos.String(); pos != "" {
		fmt.Fprintf(w, "file: %s\n", pos)
	}
	if d.Reason != "" {
		fmt.Fprintf(w, "\nReason:\n%s\n", indent(d.Reason, "  "))
	}
	if d.Suggestion != "" {
		fmt.Fprintf(w, "\nSuggestion:\n%s\n", indent(d.Suggestion, "  "))
	}
}

// RenderAll writes every diagnostic followed by a summary line.
func (b *Bag) RenderAll(w io.Writer) {
	for i, d := range b.Sorted() {
		if i > 0 {
			fmt.Fprintln(w)
		}
		d.RenderText(w)
	}
	info, warn, errs := b.Counts()
	if len(b.items) > 0 {
		fmt.Fprintln(w)
	}
	fmt.Fprintf(w, "%d error(s), %d warning(s), %d info\n", errs, warn, info)
}

func indent(s, prefix string) string {
	lines := strings.Split(strings.TrimRight(s, "\n"), "\n")
	for i, l := range lines {
		lines[i] = prefix + l
	}
	return strings.Join(lines, "\n")
}
