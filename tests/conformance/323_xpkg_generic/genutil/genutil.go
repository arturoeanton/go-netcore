package genutil

type Ordered interface {
	~int | ~int64 | ~float64 | ~string
}

// Sort's element type E is determined only through the S ~[]E constraint — the
// shape that exercises cross-package generic instantiation with the template's
// own type info.
func Sort[S ~[]E, E Ordered](x S) {
	for i := 1; i < len(x); i++ {
		for j := i; j > 0 && x[j] < x[j-1]; j-- {
			x[j], x[j-1] = x[j-1], x[j]
		}
	}
}

func Max[S ~[]E, E Ordered](x S) E {
	m := x[0]
	for i := 1; i < len(x); i++ {
		if x[i] > m {
			m = x[i]
		}
	}
	return m
}

func Map[T, U any](xs []T, f func(T) U) []U {
	out := make([]U, len(xs))
	for i, x := range xs {
		out[i] = f(x)
	}
	return out
}
