package linker

import "os"

// runtimeConfigJSON is the host configuration for a framework-dependent .NET app.
// rollForward=LatestMajor lets a net8.0-targeted assembly run on a newer shared
// framework (e.g. only .NET 10 installed). configProperties favor fast startup for the
// short-lived programs goclr usually produces (CLI tools, tests): TieredCompilation's
// quick JIT is kept, but TieredPGO instrumentation — a steady-state optimization that adds
// startup cost — is off. Long-running servers barely notice; see docs/GAPS.md (Performance).
const runtimeConfigJSON = `{
  "runtimeOptions": {
    "tfm": "net8.0",
    "rollForward": "LatestMajor",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "8.0.0"
    },
    "configProperties": {
      "System.Runtime.TieredCompilation": true,
      "System.Runtime.TieredPGO": false
    }
  }
}
`

func writeRuntimeConfig(path string) error {
	return os.WriteFile(path, []byte(runtimeConfigJSON), 0o644)
}
