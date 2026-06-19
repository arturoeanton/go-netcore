package main

type Box struct{ V int }

func (b *Box) Set(v int) { b.V = v }
func (b Box) Value() int { return b.V }

func main() {
	b := &Box{V: 5}
	println(b.Value())
	b.Set(99)
	println(b.Value())
	println(b.V)
}
