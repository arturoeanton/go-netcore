package main

import "fmt"

// Mirrors fasthttp's argsKV/allocArg: a []struct grown by append, then resliced into
// the capacity region with &h[n] and field writes. Regression test for a grown
// capacity region holding zero structs (not nulls).
type kv struct {
	key     []byte
	value   []byte
	noValue bool
}

func alloc(h []kv) ([]kv, *kv) {
	n := len(h)
	if cap(h) > n {
		h = h[:n+1]
	} else {
		h = append(h, kv{value: []byte{}})
	}
	return h, &h[n]
}

func main() {
	var h []kv
	for i := 0; i < 12; i++ {
		var p *kv
		h, p = alloc(h)
		p.key = append(p.key[:0], byte('a'+i))
		p.value = append(p.value[:0], byte('0'+i%10))
		p.noValue = i%2 == 0
	}
	for _, e := range h {
		fmt.Printf("%s=%s(%v) ", e.key, e.value, e.noValue)
	}
	fmt.Println()

	// Reset and reuse the backing array (reslice into previously-grown capacity).
	h = h[:0]
	for i := 0; i < 5; i++ {
		var p *kv
		h, p = alloc(h)
		p.key = append(p.key[:0], byte('X'))
		p.value = append(p.value[:0], byte('A'+i))
	}
	for _, e := range h {
		fmt.Printf("%s=%s ", e.key, e.value)
	}
	fmt.Println()

	// A plain []struct grown past cap, then read across the (zeroed) capacity region.
	type point struct{ x, y int }
	var ps []point
	for i := 0; i < 6; i++ {
		ps = append(ps, point{i, i * i})
	}
	full := ps[:cap(ps)]
	fmt.Println("len", len(ps), "tail-zero", full[len(ps)-1])
}
