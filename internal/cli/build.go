package cli

import (
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/analysis"
	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/emit"
	"github.com/arturoeanton/go-netcore/internal/frontend"
	"github.com/arturoeanton/go-netcore/internal/linker"
	"github.com/arturoeanton/go-netcore/internal/lower"
)

// buildValueFlags are the build/run flags that take a value argument.
var buildValueFlags = map[string]bool{
	"o": true, "target": true, "configuration": true, "profile": true,
}

// buildFlags holds the flag surface defined by spec §4.1.
type buildFlags struct {
	output        string
	target        string
	configuration string
	profile       string
	emitIL        bool
	emitCSStubs   bool
	emitIR        bool
	emitSSA       bool
	keepTemp      bool
	noAOT         bool
	aot           bool
	trim          bool
	verbose       bool
	explain       bool
}

func registerBuildFlags(fs *flag.FlagSet) *buildFlags {
	b := &buildFlags{}
	fs.StringVar(&b.output, "o", "", "output assembly path (.dll)")
	fs.StringVar(&b.target, "target", "net8.0", "target framework")
	fs.StringVar(&b.configuration, "configuration", "debug", "debug|release")
	fs.StringVar(&b.profile, "profile", string(analysis.ProfileEchoGoja), "compatibility profile")
	fs.BoolVar(&b.emitIL, "emit-il", false, "emit textual IL alongside the assembly")
	fs.BoolVar(&b.emitCSStubs, "emit-cs-stubs", false, "emit C# stubs for inspection")
	fs.BoolVar(&b.emitIR, "emit-ir", false, "emit GoCLR IR")
	fs.BoolVar(&b.emitSSA, "emit-ssa", false, "emit Go SSA")
	fs.BoolVar(&b.keepTemp, "keep-temp", false, "keep temporary build artifacts")
	fs.BoolVar(&b.noAOT, "no-aot", false, "disable AOT")
	fs.BoolVar(&b.aot, "aot", false, "enable AOT (post-MVP)")
	fs.BoolVar(&b.trim, "trim", false, "trim unused assemblies")
	fs.BoolVar(&b.verbose, "verbose", false, "verbose output")
	fs.BoolVar(&b.explain, "explain", false, "explain compilation decisions")
	return b
}

func cmdBuild(args []string) int {
	fs := flag.NewFlagSet("build", flag.ContinueOnError)
	bf := registerBuildFlags(fs)
	flags, patterns := splitArgs(args, buildValueFlags)
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		fmt.Fprintln(os.Stderr, "goclr build: expected a package pattern, e.g. ./cmd/server")
		return 2
	}

	out := bf.output
	if out == "" {
		out = defaultOutput(patterns[0])
	}

	code, _ := buildToAssembly(patterns, bf, out)
	return code
}

// buildToAssembly runs the front half of the pipeline (load → analyze) and then
// reports backend status. It returns the process exit code and the output path.
//
// The IL emission backend (lower → emit → link) is under active development; see
// docs/ROADMAP.md. Until it lands, build performs a real compatibility gate and then
// reports clearly that emission is pending, rather than producing a broken DLL.
func buildToAssembly(patterns []string, bf *buildFlags, out string) (int, string) {
	return buildToAssemblyMode(patterns, bf, out, false)
}

// buildToAssemblyMode builds patterns to an assembly. When tests is set, test files are
// loaded and the synthesized `*.test` main package (go/test's generated runner, driven by
// the goclr `testing` overlay) is the build root.
func buildToAssemblyMode(patterns []string, bf *buildFlags, out string, tests bool) (int, string) {
	patterns = normalizePatterns(patterns)
	res, err := frontend.Load(frontend.LoadConfig{Dir: ".", Patterns: patterns, Tests: tests})
	if err != nil {
		fmt.Fprintf(os.Stderr, "goclr build: %v\n", err)
		return 1, out
	}

	// Locate the build root among the loaded roots. In test mode it is the synthesized
	// `*.test` runner package; otherwise it is the user's `main` package.
	var mainPkg *frontend.Package
	for _, r := range res.Roots {
		if tests {
			if strings.HasSuffix(r.PkgPath, ".test") {
				mainPkg = r
				break
			}
		} else if r.Name == "main" {
			mainPkg = r
			break
		}
	}
	if mainPkg == nil {
		if tests {
			fmt.Fprintf(os.Stderr, "error GCLR0104: no tests found in %s\n", strings.Join(patterns, " "))
			return 1, out
		}
		fmt.Fprintf(os.Stderr, "error GCLR0104: no main package found in %s\n", strings.Join(patterns, " "))
		return 1, out
	}

	rep := analysis.Analyze(res, analysis.Profile(bf.profile))
	if !rep.Compatible {
		var sb strings.Builder
		rep.RenderDiagnostics(&sb)
		fmt.Fprint(os.Stderr, sb.String())
		fmt.Fprintln(os.Stderr, "\ngoclr build: aborting — package is not compatible with profile", bf.profile)
		return 1, out
	}

	// Lower the main package to GoCLR IR (M0 subset).
	bag := &diagnostics.Bag{}
	prog, ok := lower.Lower(mainPkg, bag)
	if !ok {
		var sb strings.Builder
		bag.RenderAll(&sb)
		fmt.Fprint(os.Stderr, sb.String())
		return 3, out
	}
	prog.AssemblyName = strings.TrimSuffix(filepath.Base(out), filepath.Ext(out))

	if bf.verbose {
		fmt.Printf("goclr build: lowered %d method(s)\n", len(prog.Methods))
		fmt.Printf("goclr build: target=%s configuration=%s output=%s\n", bf.target, bf.configuration, out)
	}

	// Emit the assembly and link the runtime + host config.
	if err := os.MkdirAll(filepath.Dir(out), 0o755); err != nil {
		fmt.Fprintf(os.Stderr, "goclr build: %v\n", err)
		return 1, out
	}
	if err := emit.Emit(prog, out); err != nil {
		fmt.Fprintf(os.Stderr, "error GCLR0500: %v\n", err)
		return 1, out
	}
	if err := linker.Link(out); err != nil {
		fmt.Fprintf(os.Stderr, "error GCLR0600: %v\n", err)
		return 1, out
	}

	if bf.verbose {
		fmt.Printf("goclr build: wrote %s\n", out)
	}
	return 0, out
}

// defaultOutput derives a .dll path from a package pattern.
func defaultOutput(pattern string) string {
	base := filepath.Base(strings.TrimRight(pattern, "/."))
	if base == "" || base == "." || base == "..." {
		base = "out"
	}
	return filepath.Join("bin", base+".dll")
}

// normalizePatterns makes a bare local file/directory path usable as a go/packages
// pattern. `go/packages` treats `examples/demo` (no `./`) as an import path, not a
// directory, so `goclr run examples/demo` failed with "no main package found" even
// though `goclr run ./examples/demo` (and the explicit `.../main.go` file) worked.
// A pattern that names an existing on-disk path and isn't already in package-pattern
// form gets a `./` prefix; import paths and `./...` wildcards are left untouched.
func normalizePatterns(patterns []string) []string {
	out := make([]string, len(patterns))
	for i, p := range patterns {
		out[i] = p
		if p == "" || strings.HasPrefix(p, ".") || strings.HasPrefix(p, "/") ||
			strings.Contains(p, "...") || filepath.IsAbs(p) {
			continue
		}
		if _, err := os.Stat(p); err == nil {
			out[i] = "." + string(filepath.Separator) + p
		}
	}
	return out
}
