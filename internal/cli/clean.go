package cli

import (
	"flag"
	"fmt"
	"os"
	"path/filepath"
)

func cmdClean(args []string) int {
	fs := flag.NewFlagSet("clean", flag.ContinueOnError)
	dryRun := fs.Bool("n", false, "show what would be removed without removing")
	if err := fs.Parse(args); err != nil {
		return 2
	}

	removed := 0

	// Whole directories.
	for _, dir := range []string{".goclr", ".goclr-cache"} {
		if _, err := os.Stat(dir); err == nil {
			if *dryRun {
				fmt.Printf("would remove %s/\n", dir)
			} else if err := os.RemoveAll(dir); err != nil {
				fmt.Fprintf(os.Stderr, "goclr clean: %v\n", err)
				return 1
			} else {
				fmt.Printf("removed %s/\n", dir)
			}
			removed++
		}
	}

	// bin/*.goclr.* artifacts.
	matches, _ := filepath.Glob(filepath.Join("bin", "*.goclr.*"))
	for _, m := range matches {
		if *dryRun {
			fmt.Printf("would remove %s\n", m)
		} else if err := os.Remove(m); err != nil {
			fmt.Fprintf(os.Stderr, "goclr clean: %v\n", err)
			return 1
		} else {
			fmt.Printf("removed %s\n", m)
		}
		removed++
	}

	if removed == 0 {
		fmt.Println("goclr clean: nothing to remove")
	}
	return 0
}
