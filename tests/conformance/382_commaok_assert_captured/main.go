// A comma-ok type-assertion result captured by a closure must live in a shared cell,
// like any captured local. The earlier bug stored the raw value (overwriting the cell),
// so a captured interface holding a pointer was double-dereferenced at dispatch.
package main

type Closer interface{ Close() string }
type Execer interface{ Exec() string }

type conn struct{ name string }

func (c *conn) Close() string { return "closed:" + c.name }
func (c *conn) Exec() string  { return "exec:" + c.name }

func main() {
	var ci Closer = &conn{name: "db"}
	ex, ok := ci.(Execer) // comma-ok interface-to-interface assertion
	if !ok {
		println("not execer")
		return
	}
	out := ""
	func() { out = ex.Exec() }() // ex captured by the closure
	println(out)
}
