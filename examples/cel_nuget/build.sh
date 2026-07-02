#!/usr/bin/env bash
# Build the Cel.GoCLR NuGet end to end:
#   1. compile the Go host (cel-go) to a .NET assembly with goclr,
#   2. build the C# facade + demo against it,
#   3. run the demo (CEL evaluated on the CLR from C#).
#
# Run from examples/cel_nuget. Requires: a built bin/goclr, Go 1.24+, .NET SDK.
set -euo pipefail
cd "$(dirname "$0")"
ROOT=$(cd ../.. && pwd)
GOCLR="$ROOT/bin/goclr"
OUT="$PWD/bin/goclr"

export GOCLR_RUNTIME_DLL="$ROOT/runtime/dotnet/bin/Release/net8.0/GoCLR.Runtime.dll"
export GOCLR_STDLIB_DLL="$ROOT/runtime/stdlib/bin/Release/net8.0/GoCLR.Stdlib.dll"

echo "==> compiling the Go cel-go host with goclr"
mkdir -p "$OUT"
( cd host && "$GOCLR" build -o "$OUT/host.dll" . )

echo "==> building the C# facade + demo"
dotnet build -c Release -v q --nologo CelDemo.csproj "/p:GoclrOut=$OUT"

# The goclr runtimeconfig rolls forward to the installed .NET major.
cp "$OUT"/*.dll bin/Release/net8.0/
python3 - "bin/Release/net8.0/CelDemo.runtimeconfig.json" <<'PY'
import json, sys
p = sys.argv[1]
d = json.load(open(p))
d["runtimeOptions"]["rollForward"] = "LatestMajor"
# QuickJitForLoops keeps the first CEL/ANTLR parse fast: without it .NET
# full-optimization-JITs every loop-bearing ANTLR method on the cold path.
cp = d["runtimeOptions"].setdefault("configProperties", {})
cp["System.Runtime.TieredCompilation"] = True
cp["System.Runtime.TieredCompilation.QuickJitForLoops"] = True
json.dump(d, open(p, "w"), indent=2)
PY

echo "==> running the demo (CEL from C#)"
dotnet bin/Release/net8.0/CelDemo.dll
