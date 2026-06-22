// Package mixed is a goclr-test fixture: a tiny package with passing, failing,
// skipping, and subtest cases used to validate `goclr test`.
package mixed

func Add(a, b int) int { return a + b }
func Mul(a, b int) int { return a * b }
