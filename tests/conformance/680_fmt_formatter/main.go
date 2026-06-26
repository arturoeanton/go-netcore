package main

import "fmt"

// A type implementing fmt.Formatter (Format(fmt.State, rune)) controls every verb's
// rendering, exactly as Go does. The State it is handed captures output (fmt.Fprintf(f,…)
// writes to it) and reports the verb's width/precision/flags. Honored at the top level,
// through a pointer, and per-element inside a slice. Previously goclr ignored Format().
type Money int // cents

func (m Money) Format(f fmt.State, verb rune) {
	switch verb {
	case 'd', 'v':
		s := fmt.Sprintf("$%d.%02d", int(m)/100, int(m)%100)
		if w, ok := f.Width(); ok {
			for len(s) < w {
				s = " " + s
			}
		}
		if f.Flag('+') {
			s = "+" + s
		}
		fmt.Fprint(f, s)
	default:
		fmt.Fprintf(f, "Money(%d)", int(m))
	}
}

// pointer-receiver Formatter
type Buf struct{ n int }

func (b *Buf) Format(f fmt.State, verb rune) { fmt.Fprintf(f, "<buf:%d>", b.n) }

func main() {
	m := Money(12345)
	fmt.Printf("%v\n", m)
	fmt.Printf("%d\n", m)
	fmt.Printf("%10v\n", m) // width read via State.Width()
	fmt.Printf("%+v\n", m)  // flag read via State.Flag('+')
	fmt.Printf("%s\n", m)   // default branch
	fmt.Println(m)

	b := &Buf{7}
	fmt.Printf("%v\n", b)
	fmt.Println(b)

	fmt.Printf("%v\n", []Money{100, 250})
	fmt.Printf("%d\n", []Money{100, 250})
}
