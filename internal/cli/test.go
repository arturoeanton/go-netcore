package cli

import (
	"flag"
	"fmt"
	"os"

	"github.com/arturoeanton/go-netcore/internal/analysis"
	"github.com/arturoeanton/go-netcore/internal/frontend"
)

func cmdTest(args []string) int {
	fs := flag.NewFlagSet("test", flag.ContinueOnError)
	profile := fs.String("profile", string(analysis.ProfileEchoGoja), "compatibility profile")
	flags, patterns := splitArgs(args, map[string]bool{"profile": true})
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		patterns = []string{"./..."}
	}

	// Load with tests so *_test.go files participate in compatibility analysis.
	res, err := frontend.Load(frontend.LoadConfig{Dir: ".", Patterns: normalizePatterns(patterns), Tests: true})
	if err != nil {
		fmt.Fprintf(os.Stderr, "goclr test: %v\n", err)
		return 1
	}
	rep := analysis.Analyze(res, analysis.Profile(*profile))
	if !rep.Compatible {
		fmt.Fprintln(os.Stderr, "goclr test: test packages are not compatible with profile", *profile)
		return 1
	}

	// Executing compiled tests requires the IL backend (testing.T harness).
	fmt.Fprintf(os.Stderr, "error GCLR0500: test execution requires the IL emission backend\n")
	fmt.Fprintf(os.Stderr, "\nReason:\n  Test packages are compatible with profile %s, but running them on .NET\n", *profile)
	fmt.Fprintf(os.Stderr, "  needs the testing.T runtime harness, which lands with the backend (M2+).\n")
	return 3
}
