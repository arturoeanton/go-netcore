package main

type Stack[T any] struct{ items []T }

func (s *Stack[T]) Push(x T) { s.items = append(s.items, x) }
func (s *Stack[T]) Len() int { return len(s.items) }
func (s *Stack[T]) Top() T   { return s.items[len(s.items)-1] }

type Pair[A, B any] struct {
	first  A
	second B
}

func (p Pair[A, B]) First() A  { return p.first }
func (p Pair[A, B]) Second() B { return p.second }

type List[T any] struct{ xs []T }

func (l *List[T]) Add(x T)        { l.xs = append(l.xs, x) }
func (l *List[T]) Get(i int) T    { return l.xs[i] }
func (l *List[T]) Each(f func(T)) {
	for _, x := range l.xs {
		f(x)
	}
}

func main() {
	var st Stack[int]
	st.Push(1)
	st.Push(2)
	st.Push(3)
	println(st.Len(), st.Top())

	var ss Stack[string]
	ss.Push("a")
	ss.Push("b")
	println(ss.Len(), ss.Top())

	p := Pair[int, string]{first: 7, second: "seven"}
	println(p.First(), p.Second())

	var l List[int]
	l.Add(10)
	l.Add(20)
	l.Add(30)
	sum := 0
	l.Each(func(x int) { sum += x })
	println(l.Get(1), sum)

	var lp List[Pair[int, int]]
	lp.Add(Pair[int, int]{first: 1, second: 2})
	lp.Add(Pair[int, int]{first: 3, second: 4})
	println(lp.Get(0).First(), lp.Get(1).Second())
}
