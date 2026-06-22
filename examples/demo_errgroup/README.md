# demo_errgroup

`golang.org/x/sync/errgroup` compiled and run on the CLR by goclr: concurrent goroutines
coordinated by an `errgroup.Group`, with first-error propagation. errgroup compiles from
source (it uses `sync` + `context`, including `context.WithCancelCause`).

```bash
go mod vendor                  # errgroup is a vendored dependency
goclr run ./examples/demo_errgroup
# squares: [1 4 9 16 25]
# first error: task failed
```
