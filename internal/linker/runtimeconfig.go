package linker

import "os"

// runtimeConfigJSON is the host configuration for a framework-dependent .NET app.
// rollForward=LatestMajor lets a net8.0-targeted assembly run on a newer shared
// framework (e.g. only .NET 10 installed).
const runtimeConfigJSON = `{
  "runtimeOptions": {
    "tfm": "net8.0",
    "rollForward": "LatestMajor",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "8.0.0"
    }
  }
}
`

func writeRuntimeConfig(path string) error {
	return os.WriteFile(path, []byte(runtimeConfigJSON), 0o644)
}
