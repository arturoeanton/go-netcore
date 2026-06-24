package cli

import (
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"sort"

	"github.com/arturoeanton/go-netcore/internal/analysis"
)

func cmdCoverage(args []string) int {
	fs := flag.NewFlagSet("coverage", flag.ContinueOnError)
	asJSON := fs.Bool("json", false, "emit the matrix as JSON")
	showMissing := fs.Bool("missing", false, "list each package's uncovered symbols")
	sortByGap := fs.Bool("gap", false, "sort packages by uncovered count (most work first)")
	out := fs.String("o", "", "write the report to this file instead of stdout")
	flags, patterns := splitArgs(args, map[string]bool{"o": true})
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		patterns = analysis.DefaultCoveragePackages()
	}

	rep, err := analysis.ComputeCoverage(patterns, false)
	if err != nil {
		fmt.Fprintf(os.Stderr, "goclr coverage: %v\n", err)
		return 1
	}

	var content string
	if *asJSON {
		b, _ := json.MarshalIndent(rep, "", "  ")
		content = string(b)
	} else {
		content = renderCoverage(rep, *showMissing, *sortByGap)
	}
	if *out != "" {
		if err := os.WriteFile(*out, []byte(content), 0o644); err != nil {
			fmt.Fprintf(os.Stderr, "goclr coverage: %v\n", err)
			return 1
		}
		fmt.Printf("wrote %s\n", *out)
		return 0
	}
	fmt.Print(content)
	return 0
}

func renderCoverage(rep *analysis.CoverageReport, showMissing, sortByGap bool) string {
	pkgs := append([]analysis.PackageCoverage(nil), rep.Packages...)
	if sortByGap {
		sort.SliceStable(pkgs, func(i, j int) bool {
			gi, gj := pkgs[i].Total-pkgs[i].Covered, pkgs[j].Total-pkgs[j].Covered
			if gi != gj {
				return gi > gj
			}
			return pkgs[i].ImportPath < pkgs[j].ImportPath
		})
	}
	var b []byte
	add := func(format string, a ...any) { b = append(b, []byte(fmt.Sprintf(format, a...))...) }

	add("goclr stdlib coverage — per-function matrix\n")
	add("%-34s %6s  %4s/%-4s  %s\n", "package", "cover", "ok", "tot", "")
	add("%s\n", dashes(78))
	for _, p := range pkgs {
		mark := "  "
		switch {
		case p.FullSource:
			mark = "src"
		case p.Percent() == 100:
			mark = " ✓ "
		}
		add("%-34s %5.0f%% %5d/%-4d %s\n", trunc(p.ImportPath, 34), p.Percent(), p.Covered, p.Total, mark)
		if showMissing && len(p.Missing) > 0 {
			for _, m := range p.Missing {
				add("        · %s\n", m)
			}
		}
	}
	add("%s\n", dashes(78))
	add("%-34s %5.0f%% %5d/%-4d  (%d packages)\n", "TOTAL", rep.Percent(), rep.Covered, rep.Total, len(rep.Packages))
	return string(b)
}

func dashes(n int) string {
	s := make([]byte, n)
	for i := range s {
		s[i] = '-'
	}
	return string(s)
}

func trunc(s string, n int) string {
	if len(s) <= n {
		return s
	}
	return s[:n-1] + "…"
}
