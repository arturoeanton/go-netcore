dotnet build runtime/stdlib/GoCLR.Stdlib.csproj -c Release
go mod vendor 
go run cmd/goclr/main.go build  examples/demo_goja -o demo.dll
dotnet demo.dll
rm demo.dll
