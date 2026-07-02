package main

import "fmt"

// A generic function passed as a VALUE with INFERRED type arguments (no explicit
// [T]): mapAll's parameter is func(string) T, so `wrap` is instantiated to
// wrap[string] with T inferred = string — the shape OPA uses
// (util.SplitMap(s, sep, ast.InternedTerm)).
func wrap[T any](v T) T { return v }

func mapAll[T any](xs []string, f func(string) T) []T {
	out := make([]T, 0, len(xs))
	for _, x := range xs {
		out = append(out, f(x))
	}
	return out
}

func main() {
	got := mapAll([]string{"a", "b", "c"}, wrap) // wrap inferred as wrap[string]
	fmt.Println("len:", len(got))
	fmt.Println("vals:", got[0], got[1], got[2])
}
