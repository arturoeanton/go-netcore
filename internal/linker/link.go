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
	return nil
}

// locateRuntimeDLL finds GoCLR.Runtime.dll, honoring GOCLR_RUNTIME_DLL, then
// searching upward from the cwd for the runtime project's build output, building
// it on demand if necessary.
func locateRuntimeDLL() (string, error) {
	if p := os.Getenv("GOCLR_RUNTIME_DLL"); p != "" {
		if _, err := os.Stat(p); err == nil {
			return p, nil
		}
	}

	csproj := findUp("runtime/dotnet/GoCLR.Runtime.csproj")
	if csproj == "" {
		return "", fmt.Errorf("could not locate runtime/dotnet/GoCLR.Runtime.csproj (set GOCLR_RUNTIME_DLL)")
	}
	projDir := filepath.Dir(csproj)

	if dll := newestDLL(filepath.Join(projDir, "bin")); dll != "" {
		return dll, nil
	}
	// Build it once. Capture output and surface it only on failure, so it never
	// pollutes the stdout/stderr of a program run via `goclr run`.
	cmd := exec.Command("dotnet", "build", "-c", "Release", "-v", "q", "--nologo", csproj)
	if out, err := cmd.CombinedOutput(); err != nil {
		return "", fmt.Errorf("building GoCLR.Runtime: %w\n%s", err, out)
	}
	if dll := newestDLL(filepath.Join(projDir, "bin")); dll != "" {
		return dll, nil
	}
	return "", fmt.Errorf("GoCLR.Runtime.dll not found after build under %s/bin", projDir)
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

// newestDLL returns the most recently modified GoCLR.Runtime.dll under root.
func newestDLL(root string) string {
	var best string
	var bestMod int64
	_ = filepath.Walk(root, func(path string, info os.FileInfo, err error) error {
		if err != nil || info.IsDir() {
			return nil
		}
		if filepath.Base(path) == "GoCLR.Runtime.dll" && info.ModTime().UnixNano() > bestMod {
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
