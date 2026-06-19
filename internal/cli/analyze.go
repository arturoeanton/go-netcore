package cli

import (
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/analysis"
	"github.com/arturoeanton/go-netcore/internal/frontend"
)

func cmdAnalyze(args []string) int {
	fs := flag.NewFlagSet("analyze", flag.ContinueOnError)
	profile := fs.String("profile", string(analysis.ProfileEchoGoja), "compatibility profile")
	asJSON := fs.Bool("json", false, "emit a machine-readable JSON report")
	verbose := fs.Bool("verbose", false, "include stdlib packages in the human report")
	flags, patterns := splitArgs(args, map[string]bool{"profile": true})
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		patterns = []string{"./..."}
	}

	res, err := frontend.Load(frontend.LoadConfig{Dir: ".", Patterns: patterns})
	if err != nil {
		fmt.Fprintf(os.Stderr, "goclr analyze: %v\n", err)
		return 1
	}

	rep := analysis.Analyze(res, analysis.Profile(*profile))

	if *asJSON {
		enc := json.NewEncoder(os.Stdout)
		enc.SetIndent("", "  ")
		if err := enc.Encode(rep); err != nil {
			fmt.Fprintf(os.Stderr, "goclr analyze: %v\n", err)
			return 1
		}
	} else {
		var sb strings.Builder
		if *verbose {
			rep.RenderTextVerbose(&sb)
		} else {
			rep.RenderText(&sb)
		}
		fmt.Print(sb.String())
	}

	if !rep.Compatible {
		return 1
	}
	return 0
}
