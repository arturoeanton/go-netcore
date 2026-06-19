package calc

var Offset = 10
var calls int

func init() { Offset += 5 }

type Accumulator struct{ total int }

func (a *Accumulator) Add(n int) { a.total += n }
func (a *Accumulator) Total() int { return a.total }

func Add(a, b int) int  { calls++; return a + b + Offset }
func Calls() int        { return calls }
func Sum(xs []int) int {
	t := 0
	for _, x := range xs {
		t += x
	}
	return t
}
