package box

// Box is a generic container defined in a separate package, exercised with
// explicit type arguments and cross-package method instantiation.
type Box[T any] struct{ items []T }

func New[T any]() *Box[T]    { return &Box[T]{} }
func (b *Box[T]) Add(x T)    { b.items = append(b.items, x) }
func (b *Box[T]) Len() int   { return len(b.items) }
func (b *Box[T]) At(i int) T { return b.items[i] }
func Map[T, U any](xs []T, f func(T) U) []U {
	out := make([]U, len(xs))
	for i, x := range xs {
		out[i] = f(x)
	}
	return out
}
