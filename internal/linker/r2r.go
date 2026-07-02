package linker

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
)

// ReadyToRun precompiles the emitted assembly — plus the GoCLR.Runtime/Stdlib
// copies the linker placed next to it — to native code with crossgen2
// (ReadyToRun images). This removes the first-run JIT cost, the dominant
// startup term for large programs (goja: ~4s JIT → ~0.1s R2R).
//
// crossgen2 ships as a NuGet pack, not with the SDK: it is looked up in the
// local NuGet cache (any version), overridable with GOCLR_CROSSGEN2. Fetch it
// once by publishing any project with -p:PublishReadyToRun=true.
func ReadyToRun(dllPath string, verbose bool) error {
	cg, err := locateCrossgen2()
	if err != nil {
		return err
	}
	fw, err := frameworkDir()
	if err != nil {
		return err
	}
	outDir := filepath.Dir(dllPath)
	targets := []string{dllPath}
	for _, extra := range []string{"GoCLR.Runtime.dll", "GoCLR.Stdlib.dll"} {
		p := filepath.Join(outDir, extra)
		if _, err := os.Stat(p); err == nil {
			targets = append(targets, p)
		}
	}
	for _, dll := range targets {
		if err := crossgenOne(cg, fw, outDir, dll, verbose); err != nil {
			return err
		}
	}
	return nil
}

// crossgenOne compiles one assembly to an R2R image in place (via a temp file,
// so a failure leaves the IL assembly untouched).
func crossgenOne(cg, fw, refDir, dll string, verbose bool) error {
	tmp := dll + ".r2r.tmp"
	args := []string{
		dll,
		"-r", fw + string(filepath.Separator),
		"-r", refDir + string(filepath.Separator),
		"--out", tmp,
		"--targetarch", r2rArch(),
		"--targetos", r2rOS(),
		"-O",
	}
	cmd := exec.Command(cg, args...)
	out, err := cmd.CombinedOutput()
	if err != nil {
		os.Remove(tmp)
		return fmt.Errorf("crossgen2 %s: %w\n%s", filepath.Base(dll), err, out)
	}
	if err := os.Rename(tmp, dll); err != nil {
		return fmt.Errorf("replacing %s with its R2R image: %w", dll, err)
	}
	if verbose {
		fmt.Printf("goclr build: crossgen2 → %s (ReadyToRun)\n", filepath.Base(dll))
	}
	return nil
}

// locateCrossgen2 finds the crossgen2 host binary: GOCLR_CROSSGEN2, then the
// newest microsoft.netcore.app.crossgen2.<rid> pack in the local NuGet cache.
func locateCrossgen2() (string, error) {
	if p := os.Getenv("GOCLR_CROSSGEN2"); p != "" {
		if _, err := os.Stat(p); err == nil {
			return p, nil
		}
	}
	home, err := os.UserHomeDir()
	if err != nil {
		return "", err
	}
	rid := r2rOS() + "-" + r2rArch()
	packDir := filepath.Join(home, ".nuget", "packages", "microsoft.netcore.app.crossgen2."+rid)
	entries, err := os.ReadDir(packDir)
	if err != nil || len(entries) == 0 {
		return "", fmt.Errorf("crossgen2 not found (looked in %s).\n"+
			"Fetch it once with: dotnet publish any project with -p:PublishReadyToRun=true -r %s,\n"+
			"or point GOCLR_CROSSGEN2 at a crossgen2 binary", packDir, rid)
	}
	var versions []string
	for _, e := range entries {
		if e.IsDir() {
			versions = append(versions, e.Name())
		}
	}
	sort.Strings(versions)
	for i := len(versions) - 1; i >= 0; i-- {
		bin := filepath.Join(packDir, versions[i], "tools", "crossgen2")
		if runtime.GOOS == "windows" {
			bin += ".exe"
		}
		if _, err := os.Stat(bin); err == nil {
			return bin, nil
		}
	}
	return "", fmt.Errorf("crossgen2 pack found in %s but no tools/crossgen2 binary", packDir)
}

// frameworkDir returns the newest installed Microsoft.NETCore.App shared
// framework directory (the reference set crossgen2 compiles against).
func frameworkDir() (string, error) {
	out, err := exec.Command("dotnet", "--list-runtimes").Output()
	if err != nil {
		return "", fmt.Errorf("dotnet --list-runtimes: %w", err)
	}
	var best, bestVer string
	for _, line := range strings.Split(string(out), "\n") {
		if !strings.HasPrefix(line, "Microsoft.NETCore.App ") {
			continue
		}
		parts := strings.SplitN(line, " ", 3)
		if len(parts) != 3 {
			continue
		}
		ver := parts[1]
		dir := strings.Trim(strings.TrimSpace(parts[2]), "[]")
		if ver > bestVer {
			bestVer, best = ver, filepath.Join(dir, ver)
		}
	}
	if best == "" {
		return "", fmt.Errorf("no Microsoft.NETCore.App runtime found via dotnet --list-runtimes")
	}
	return best, nil
}

func r2rArch() string {
	switch runtime.GOARCH {
	case "amd64":
		return "x64"
	default:
		return runtime.GOARCH
	}
}

func r2rOS() string {
	switch runtime.GOOS {
	case "darwin":
		return "osx"
	default:
		return runtime.GOOS
	}
}
