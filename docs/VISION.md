# goclr — visión y prioridades

> goclr compila Go puro a ensamblados .NET (ECMA-335 IL que corre en `dotnet`). La
> meta es **transparencia con Go**: que una librería pueda decir "mis tests pasan en
> GoCLR", con un reporte de compatibilidad paquete-por-paquete y función-por-función,
> sin hacks ocultos ni deuda técnica.

Este documento es la **visión priorizada**. El checklist vivo (qué está hecho ✅ y qué
falta) se mantiene en [`priorizar.md`](priorizar.md). El detalle de milestones está en
[`ROADMAP.md`](ROADMAP.md); los gaps en [`GAPS.md`](GAPS.md); los límites en
[`LIMITATIONS.md`](LIMITATIONS.md).

## Orden recomendado (para subir la nota más rápido)

1. `goclr test` compatible con `go test`
2. Compatibility report estable HTML/JSON
3. typed-nil en interfaces (`var p *T; any(p) == nil` ⇒ `false`)
4. `reflect.StructField.Name` / `.Tag` directo
5. function values de funciones shimmed
6. formato de panic no recuperado igual a Go
7. deep `reflect` mínimo (más goja/libs)
8. `text/template` + `google/uuid` + `errgroup` + testify
9. GORM target chico
10. performance / AOT

## Principio rector

- **Agnóstico**: el compilador implementa semántica general de Go + stdlib; los
  proyectos aportan overlays para deps difíciles. Ningún tipo de tercero se hardcodea.
- **Sin deuda técnica**: cada feature se cierra con fixture byte-exacto vs `go run`,
  validadores verdes, y documentación. Si algo queda parcial, se documenta como límite
  predecible (no falla silencioso).
- **Transparencia**: se prefiere bajar Go real a simular; los límites se reportan con
  diagnósticos accionables.
