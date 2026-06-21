// A pointer-receiver method promoted through a value-embedded field, called on a
// pointer base, must observe and mutate the live struct — including mutations it
// makes to the pointee through a back-pointer field (the shape gin's Engine uses to
// register routes). A stale whole-struct snapshot/write-back would clobber them.
package main

type group struct {
	engine *engine
}

type engine struct {
	group
	routes []string
}

func (g *group) add(r string) { g.engine.routes = append(g.engine.routes, r) }

func newEngine() *engine {
	e := &engine{}
	e.group.engine = e
	return e
}

func main() {
	e := newEngine()
	e.add("/a") // promoted method, mutates e.routes through the back-pointer
	e.add("/b")
	e.add("/c")
	println("routes:", len(e.routes))
	for i := 0; i < len(e.routes); i++ {
		println(e.routes[i])
	}
}
