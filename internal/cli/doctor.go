package cli

import (
	"flag"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
)

type checkResult struct {
	name   string
	ok     bool
	detail string
	fatal  bool // a failure here blocks goclr from working at all
}

func cmdDoctor(args []string) int {
	fs := flag.NewFlagSet("doctor", flag.ContinueOnError)
	if err := fs.Parse(args); err != nil {
		return 2
	}

	var results []checkResult
	results = append(results, checkGo())
	results = append(results, checkDotnet())
	results = append(results, checkOS())
	results = append(results, checkWritable())
	results = append(results, checkGoModCache())
	results = append(results, checkGoclrCache())

	fmt.Println("goclr doctor")
	fmt.Println()
	failedFatal := false
	for _, r := range results {
		status := "ok"
		if !r.ok {
			if r.fatal {
				status = "FAIL"
				failedFatal = true
			} else {
				status = "warn"
			}
		}
		fmt.Printf("  [%-4s] %-18s %s\n", status, r.name, r.detail)
	}
	fmt.Println()
	if failedFatal {
		fmt.Println("Result: environment is NOT ready for goclr (see FAIL above).")
		return 1
	}
	fmt.Println("Result: environment is ready for goclr.")
	return 0
}

func checkGo() checkResult {
	out, err := exec.Command("go", "version").Output()
	if err != nil {
		return checkResult{name: "go", ok: false, fatal: true, detail: "not found in PATH"}
	}
	return checkResult{name: "go", ok: true, detail: strings.TrimSpace(string(out))}
}

func checkDotnet() checkResult {
	out, err := exec.Command("dotnet", "--version").Output()
	if err != nil {
		return checkResult{name: "dotnet", ok: false, fatal: true, detail: "dotnet SDK not found in PATH"}
	}
	return checkResult{name: "dotnet", ok: true, detail: ".NET SDK " + strings.TrimSpace(string(out))}
}

func checkOS() checkResult {
	return checkResult{name: "os", ok: true, detail: fmt.Sprintf("%s/%s", runtime.GOOS, runtime.GOARCH)}
}

func checkWritable() checkResult {
	dir := ".goclr"
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return checkResult{name: "write access", ok: false, fatal: true, detail: err.Error()}
	}
	probe := filepath.Join(dir, ".doctor-probe")
	if err := os.WriteFile(probe, []byte("ok"), 0o644); err != nil {
		return checkResult{name: "write access", ok: false, fatal: true, detail: err.Error()}
	}
	_ = os.Remove(probe)
	return checkResult{name: "write access", ok: true, detail: "working directory is writable"}
}

func checkGoModCache() checkResult {
	out, err := exec.Command("go", "env", "GOMODCACHE").Output()
	if err != nil {
		return checkResult{name: "go mod cache", ok: false, detail: "could not query GOMODCACHE"}
	}
	path := strings.TrimSpace(string(out))
	if path == "" {
		return checkResult{name: "go mod cache", ok: false, detail: "GOMODCACHE is empty"}
	}
	if _, err := os.Stat(path); err != nil {
		return checkResult{name: "go mod cache", ok: false, detail: path + " (not present yet)"}
	}
	return checkResult{name: "go mod cache", ok: true, detail: path}
}

func checkGoclrCache() checkResult {
	dir := ".goclr-cache"
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return checkResult{name: "goclr cache", ok: false, detail: err.Error()}
	}
	return checkResult{name: "goclr cache", ok: true, detail: dir}
}
