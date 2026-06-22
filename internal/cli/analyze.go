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
	asHTML := fs.Bool("html", false, "emit a self-contained HTML compatibility report")
	out := fs.String("o", "", "write the report to this file instead of stdout")
	verbose := fs.Bool("verbose", false, "include stdlib packages in the human report")
	flags, patterns := splitArgs(args, map[string]bool{"profile": true, "o": true})
	if err := fs.Parse(flags); err != nil {
		return 2
	}
	if len(patterns) == 0 {
		patterns = []string{"./..."}
	}

	res, err := frontend.Load(frontend.LoadConfig{Dir: ".", Patterns: normalizePatterns(patterns)})
	if err != nil {
		fmt.Fprintf(os.Stderr, "goclr analyze: %v\n", err)
		return 1
	}

	rep := analysis.Analyze(res, analysis.Profile(*profile))

	// Render to the chosen format.
	var content string
	switch {
	case *asJSON:
		var sb strings.Builder
		enc := json.NewEncoder(&sb)
		enc.SetIndent("", "  ")
		if err := enc.Encode(rep); err != nil {
			fmt.Fprintf(os.Stderr, "goclr analyze: %v\n", err)
			return 1
		}
		content = sb.String()
	case *asHTML:
		var sb strings.Builder
		rep.RenderHTML(&sb)
		content = sb.String()
	default:
		var sb strings.Builder
		if *verbose {
			rep.RenderTextVerbose(&sb)
		} else {
			rep.RenderText(&sb)
		}
		content = sb.String()
	}

	// Emit to a file (-o) or stdout.
	if *out != "" {
		if err := os.WriteFile(*out, []byte(content), 0o644); err != nil {
			fmt.Fprintf(os.Stderr, "goclr analyze: %v\n", err)
			return 1
		}
		fmt.Fprintf(os.Stderr, "goclr analyze: wrote %s\n", *out)
	} else {
		fmt.Print(content)
	}

	if !rep.Compatible {
		return 1
	}
	return 0
}
