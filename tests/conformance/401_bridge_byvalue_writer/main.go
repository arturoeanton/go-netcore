package main

import "fmt"

// counter is a VALUE-receiver io.Writer accumulating through a *int. There is no known
// sink (bytes.Buffer/os.File) to shortcut to, so fmt.Fprintf must drive counter.Write
// through the interface method-callback bridge — once as a bare struct value, once as a
// pointer (&c). Both forms must reach the same Write.
type counter struct {
	n     *int
	calls *int
}

func (c counter) Write(p []byte) (int, error) {
	*c.n += len(p)
	*c.calls++
	return len(p), nil
}

func main() {
	var n, calls int
	c := counter{n: &n, calls: &calls}

	// by value
	fmt.Fprintf(c, "x=%d;", 42) // "x=42;" -> 5 bytes
	fmt.Fprint(c, "abcd")       // 4 bytes

	// by pointer (a *counter is also an io.Writer via the value-receiver method set)
	fmt.Fprintf(&c, "%s", "hello") // 5 bytes

	fmt.Println("bytes:", n, "calls:", calls)
}
