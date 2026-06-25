// Package linker finishes a goclr build: it writes the .NET host configuration
// (runtimeconfig.json) next to the emitted assembly and copies the GoCLR runtime
// assembly into the output directory so `dotnet app.dll` can run standalone.
package linker

import (
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

// Link writes runtimeconfig.json beside dllPath and copies GoCLR.Runtime.dll into
// the same directory, building the runtime first if a prebuilt copy is not found.
func Link(dllPath string) error {
	outDir := filepath.Dir(dllPath)
	base := strings.TrimSuffix(filepath.Base(dllPath), filepath.Ext(dllPath))

	if err := writeRuntimeConfig(filepath.Join(outDir, base+".runtimeconfig.json")); err != nil {
		return err
	}

	runtimeDLL, err := locateRuntimeDLL()
	if err != nil {
		return err
	}
	if err := copyFile(runtimeDLL, filepath.Join(outDir, "GoCLR.Runtime.dll")); err != nil {
		return fmt.Errorf("copying runtime: %w", err)
	}
	// Copy the stdlib shim assembly if available (only needed when the program
	// uses shimmed stdlib packages, but harmless otherwise).
	if stdlibDLL, err := locateStdlibDLL(); err == nil {
		if err := copyFile(stdlibDLL, filepath.Join(outDir, "GoCLR.Stdlib.dll")); err != nil {
			return fmt.Errorf("copying stdlib: %w", err)
		}
	}
	return nil
}

// locateRuntimeDLL finds GoCLR.Runtime.dll, honoring GOCLR_RUNTIME_DLL, then
// searching upward from the cwd for the runtime project's build output, building
// it on demand if necessary.
func locateRuntimeDLL() (string, error) {
	return locateDLL("GoCLR.Runtime", "GOCLR_RUNTIME_DLL", "runtime/dotnet/GoCLR.Runtime.csproj")
}

// locateStdlibDLL finds GoCLR.Stdlib.dll, honoring GOCLR_STDLIB_DLL.
func locateStdlibDLL() (string, error) {
	return locateDLL("GoCLR.Stdlib", "GOCLR_STDLIB_DLL", "runtime/stdlib/GoCLR.Stdlib.csproj")
}

// locateDLL finds a managed assembly by name, honoring its env override, then
// searching upward for its project's build output, building on demand.
func locateDLL(name, envVar, csprojRel string) (string, error) {
	if p := os.Getenv(envVar); p != "" {
		if _, err := os.Stat(p); err == nil {
			return p, nil
		}
	}
	csproj := findUp(csprojRel)
	if csproj == "" {
		return "", fmt.Errorf("could not locate %s (set %s)", csprojRel, envVar)
	}
	projDir := filepath.Dir(csproj)
	dllName := name + ".dll"
	// Use a cached build only when it is up to date with the runtime sources. After a
	// `git pull`, the bin/ DLL predates the changed .cs files, and a compiled program
	// that references a newly added runtime method would fail at load with
	// MissingMethodException; rebuild instead of silently using the stale assembly.
	if dll := newestNamedDLL(filepath.Join(projDir, "bin"), dllName); dll != "" && !runtimeSourceNewer(dll, projDir) {
		return dll, nil
	}
	cmd := exec.Command("dotnet", "build", "-c", "Release", "-v", "q", "--nologo", csproj)
	if out, err := cmd.CombinedOutput(); err != nil {
		return "", fmt.Errorf("building %s: %w\n%s", name, err, out)
	}
	if dll := newestNamedDLL(filepath.Join(projDir, "bin"), dllName); dll != "" {
		return dll, nil
	}
	return "", fmt.Errorf("%s not found after build under %s/bin", dllName, projDir)
}

// runtimeSourceNewer reports whether any C#/project source under the runtime tree is
// newer than the built DLL — i.e. the cached build is stale. projDir is the assembly's
// project directory (e.g. runtime/stdlib); its parent (runtime/) also holds the
// referenced GoCLR.Runtime project, so the whole tree is scanned. Errors are treated as
// "stale" so a rebuild is attempted rather than risking a MissingMethod load failure.
func runtimeSourceNewer(dll, projDir string) bool {
	di, err := os.Stat(dll)
	if err != nil {
		return true
	}
	dllMod := di.ModTime()
	root := filepath.Dir(projDir) // .../runtime — covers stdlib + dotnet/Runtime
	newer := false
	_ = filepath.Walk(root, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return nil
		}
		if info.IsDir() {
			if base := filepath.Base(path); base == "bin" || base == "obj" {
				return filepath.SkipDir // skip build output (and the DLL itself)
			}
			return nil
		}
		if ext := filepath.Ext(path); ext == ".cs" || ext == ".csproj" {
			if info.ModTime().After(dllMod) {
				newer = true
				return filepath.SkipAll
			}
		}
		return nil
	})
	return newer
}

// findUp walks up from the cwd looking for a relative path, returning its
// absolute form or "".
func findUp(rel string) string {
	dir, err := os.Getwd()
	if err != nil {
		return ""
	}
	for {
		cand := filepath.Join(dir, rel)
		if _, err := os.Stat(cand); err == nil {
			return cand
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			return ""
		}
		dir = parent
	}
}

// newestNamedDLL returns the most recently modified DLL of the given name under root.
func newestNamedDLL(root, name string) string {
	var best string
	var bestMod int64
	_ = filepath.Walk(root, func(path string, info os.FileInfo, err error) error {
		if err != nil || info.IsDir() {
			return nil
		}
		if filepath.Base(path) == name && info.ModTime().UnixNano() > bestMod {
			best = path
			bestMod = info.ModTime().UnixNano()
		}
		return nil
	})
	return best
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()
	if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
		return err
	}
	out, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer out.Close()
	if _, err := io.Copy(out, in); err != nil {
		return err
	}
	return out.Close()
}
