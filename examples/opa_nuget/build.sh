#!/usr/bin/env bash
# Build the Opa.GoCLR NuGet end to end:
#   1. compile the Go host (open-policy-agent/opa) to a .NET assembly with goclr,
#   2. build the C# facade + demo against it,
#   3. run the demo (Rego evaluated on the CLR from C#).
#
# Run from examples/opa_nuget. Requires: a built bin/goclr, Go 1.24+, .NET SDK.
set -euo pipefail
cd "$(dirname "$0")"
ROOT=$(cd ../.. && pwd)
GOCLR="$ROOT/bin/goclr"
OUT="$PWD/bin/goclr"

export GOCLR_RUNTIME_DLL="$ROOT/runtime/dotnet/bin/Release/net8.0/GoCLR.Runtime.dll"
export GOCLR_STDLIB_DLL="$ROOT/runtime/stdlib/bin/Release/net8.0/GoCLR.Stdlib.dll"

echo "==> compiling the Go open-policy-agent/opa host with goclr"
mkdir -p "$OUT"
# Vendor so goclr can apply host/goclr.overlays (the logrus/xxhash/capabilities/
# schemas/go-metrics/fastjson/pbkdf2/jwx stubs) over the vendored dependencies
# before lowering.
( cd host && go mod vendor && "$GOCLR" build -o "$OUT/host.dll" . )

echo "==> building the C# facade + demo"
dotnet build -c Release -v q --nologo OpaDemo.csproj "/p:GoclrOut=$OUT"

# The goclr runtimeconfig rolls forward to the installed .NET major.
cp "$OUT"/*.dll bin/Release/net8.0/
python3 - "bin/Release/net8.0/OpaDemo.runtimeconfig.json" <<'PY'
import json, sys
p = sys.argv[1]
d = json.load(open(p))
d["runtimeOptions"]["rollForward"] = "LatestMajor"
# QuickJitForLoops keeps the first eval fast: without it .NET
# full-optimization-JITs every loop-bearing scanner/parser/topdown method on the
# cold path.
cp = d["runtimeOptions"].setdefault("configProperties", {})
cp["System.Runtime.TieredCompilation"] = True
cp["System.Runtime.TieredCompilation.QuickJitForLoops"] = True
json.dump(d, open(p, "w"), indent=2)
PY

echo "==> running the demo (Rego from C#)"
dotnet bin/Release/net8.0/OpaDemo.dll
