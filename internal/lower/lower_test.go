package lower

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
	"github.com/arturoeanton/go-netcore/internal/goir"
)

// loadMain writes a temp module with the given main.go and loads its main package.
func loadMain(t *testing.T, src string) *frontend.Package {
	t.Helper()
	dir := t.TempDir()
	if err := os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module tmp\n\ngo 1.21\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(dir, "main.go"), []byte(src), 0o644); err != nil {
		t.Fatal(err)
	}
	res, err := frontend.Load(frontend.LoadConfig{Dir: dir, Patterns: []string{"."}})
	if err != nil {
		t.Fatalf("load: %v", err)
	}
	for _, r := range res.Roots {
		if r.Name == "main" {
			return r
		}
	}
	t.Fatal("no main package loaded")
	return nil
}

// findMethod returns the lowered method with the given Go name.
func findMethod(prog *goir.Program, name string) *goir.Method {
	for _, m := range prog.Methods {
		if m.GoName == name {
			return m
		}
	}
	return nil
}

func countOp(m *goir.Method, code goir.Opcode) int {
	n := 0
	for _, op := range m.Code {
		if op.Code == code {
			n++
		}
	}
	return n
}

func TestLowerPrintAndCall(t *testing.T) {
	pkg := loadMain(t, `package main

func add(a, b int) int { return a + b }

func main() {
	println("a", "b")
	println(add(2, 3))
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	if main == nil {
		t.Fatal("no main method")
	}
	if got := countOp(main, goir.OpCallPrintln); got != 2 {
		t.Errorf("println calls = %d, want 2", got)
	}
	if got := countOp(main, goir.OpCallMethod); got != 1 {
		t.Errorf("method calls = %d, want 1 (add)", got)
	}
	// 3 boxes: two GoString args ("a","b") and the int result of add(2,3).
	if got := countOp(main, goir.OpBox); got != 3 {
		t.Errorf("box ops = %d, want 3 (two strings + one int)", got)
	}
	add := findMethod(prog, "add")
	if add == nil || add.Ret != goir.TInt64 || len(add.Params) != 2 {
		t.Errorf("add signature wrong: %+v", add)
	}
}

func TestLowerControlFlow(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	sum := 0
	for i := 0; i < 5; i++ {
		if i%2 == 0 {
			sum += i
		}
	}
	println(sum)
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	if countOp(main, goir.OpBr)+countOp(main, goir.OpBrFalse)+countOp(main, goir.OpBrTrue) == 0 {
		t.Error("expected branches from for/if lowering")
	}
}

func TestLowerStrings(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	s := "áb"
	println(len(s))
	println(s[0])
	println(s + "!")
	println(s == "x")
	for i, r := range s {
		println(i, r)
	}
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	for _, want := range []goir.Opcode{
		goir.OpStrConst, goir.OpStrLen, goir.OpStrIndex,
		goir.OpStrConcat, goir.OpStrEqual, goir.OpStrRuneAt, goir.OpStrRuneSize,
	} {
		if countOp(main, want) == 0 {
			t.Errorf("expected at least one %v in lowered string program", want)
		}
	}
}

func TestLowerStructs(t *testing.T) {
	pkg := loadMain(t, `package main

type Point struct{ X, Y int }

func main() {
	p := Point{X: 1, Y: 2}
	p.X = 5
	println(p.X + p.Y)
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	if len(prog.Structs) != 1 {
		t.Fatalf("got %d structs, want 1", len(prog.Structs))
	}
	s := prog.Structs[0]
	if s.GoName != "Point" || len(s.Fields) != 2 {
		t.Errorf("struct = %+v, want Point with 2 fields", s)
	}
	if s.Fields[0].Name != "X" || s.Fields[0].Type != goir.TInt64 {
		t.Errorf("field 0 = %+v, want X int64", s.Fields[0])
	}
	main := findMethod(prog, "main")
	for _, want := range []goir.Opcode{goir.OpInitObj, goir.OpStFld, goir.OpLdFld, goir.OpLdLocA} {
		if countOp(main, want) == 0 {
			t.Errorf("expected at least one %v in lowered struct program", want)
		}
	}
}

func TestLowerSlices(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	s := make([]int, 2)
	s[0] = 1
	s = append(s, 9)
	println(len(s), cap(s), s[0])
	for _, v := range s {
		println(v)
	}
	b := []byte("hi")
	println(len(b))
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	for _, want := range []goir.Opcode{
		goir.OpSliceMake, goir.OpSliceSet, goir.OpSliceGet, goir.OpSliceAppend,
		goir.OpSliceLen, goir.OpSliceCap, goir.OpUnbox, goir.OpStrToBytes,
	} {
		if countOp(main, want) == 0 {
			t.Errorf("expected at least one %v in lowered slice program", want)
		}
	}
}

func TestLowerMaps(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	m := map[string]int{"a": 1}
	m["b"] = 2
	println(len(m), m["a"])
	v, ok := m["b"]
	println(v, ok)
	delete(m, "a")
	for k, val := range m {
		println(k, val)
	}
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	for _, want := range []goir.Opcode{
		goir.OpMapMake, goir.OpMapSet, goir.OpMapGet, goir.OpMapContains,
		goir.OpMapLen, goir.OpMapDelete, goir.OpMapKeys,
	} {
		if countOp(main, want) == 0 {
			t.Errorf("expected at least one %v in lowered map program", want)
		}
	}
}

func TestLowerPointers(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	x := 1
	p := &x
	*p = 5
	println(x)
	q := new(int)
	println(*q)
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	for _, want := range []goir.Opcode{goir.OpPtrNew, goir.OpPtrGet, goir.OpPtrSet} {
		if countOp(main, want) == 0 {
			t.Errorf("expected at least one %v in lowered pointer program", want)
		}
	}
	// x is address-taken, so its local must be a GoPtr cell.
	if main.Locals[0].Kind != goir.KPtr {
		t.Errorf("address-taken local x should be a GoPtr cell, got %v", main.Locals[0].Kind)
	}
}

func TestLowerMethods(t *testing.T) {
	pkg := loadMain(t, `package main

type Rect struct{ W, H int }

func (r Rect) Area() int   { return r.W * r.H }
func (r *Rect) Grow(d int) { r.W = r.W + d }

func main() {
	r := Rect{W: 3, H: 4}
	println(r.Area())
	r.Grow(2)
	println(r.Area())
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	// Methods become static methods named Type_Method with a receiver param.
	area := findMethod(prog, "Rect_Area")
	if area == nil || len(area.Params) != 1 || area.Params[0].Kind != goir.KStruct {
		t.Fatalf("Rect_Area shell wrong: %+v", area)
	}
	grow := findMethod(prog, "Rect_Grow")
	if grow == nil || len(grow.Params) != 2 || grow.Params[0].Kind != goir.KPtr {
		t.Fatalf("Rect_Grow shell wrong: %+v", grow)
	}
	main := findMethod(prog, "main")
	if countOp(main, goir.OpCallMethod) != 3 {
		t.Errorf("expected 3 method calls (Area, Grow, Area), got %d", countOp(main, goir.OpCallMethod))
	}
	// r is address-taken (Grow has a pointer receiver) -> a cell.
	if main.Locals[0].Kind != goir.KPtr {
		t.Errorf("r should be a GoPtr cell (pointer-receiver call), got %v", main.Locals[0].Kind)
	}
}

func TestLowerMultiReturn(t *testing.T) {
	pkg := loadMain(t, `package main

func divmod(a, b int) (int, int) {
	return a / b, a % b
}

func main() {
	q, r := divmod(17, 5)
	println(q, r)
	a, b := 1, 2
	a, b = b, a
	println(a, b)
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	dm := findMethod(prog, "divmod")
	if dm == nil || dm.Ret.Kind != goir.KObjectArray || len(dm.Results) != 2 {
		t.Fatalf("divmod should return an object[] tuple: %+v", dm)
	}
	main := findMethod(prog, "main")
	if countOp(main, goir.OpLdElemRef) == 0 {
		t.Error("expected ldelem.ref for tuple unpacking")
	}
}

func TestLowerAnyInterface(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	var x any = 5
	n := x.(int)
	println(n)
	if v, ok := x.(string); ok {
		println(v)
	}
	switch t := x.(type) {
	case int:
		println(t)
	default:
		println("other")
	}
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	// x is `any` -> object; the literal 5 is boxed; assertions use isinst/unbox.
	if main.Locals[0].Kind != goir.KObject {
		t.Errorf("any local should be object, got %v", main.Locals[0].Kind)
	}
	if countOp(main, goir.OpIsInst) == 0 {
		t.Error("expected isinst for type assertion / switch")
	}
	if countOp(main, goir.OpBox) == 0 {
		t.Error("expected box for storing a concrete value into any")
	}
}

func TestLowerNamedInterface(t *testing.T) {
	pkg := loadMain(t, `package main

type Stringer interface{ String() string }
type Pt struct{ X int }
func (p Pt) String() string { return "pt" }

func main() {
	var s Stringer = Pt{X: 1}
	println(s.String())
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	// Dispatch generates isinst over implementers + a call to the concrete method.
	if countOp(main, goir.OpIsInst) == 0 {
		t.Error("expected isinst for interface dispatch")
	}
	if findMethod(prog, "Pt_String") == nil {
		t.Error("expected the concrete method Pt_String to be emitted")
	}
}

func TestLowerDeferPanicRecover(t *testing.T) {
	pkg := loadMain(t, `package main

func cleanup() { println("done") }

func main() {
	defer cleanup()
	if r := recover(); r != nil {
		println("?")
	}
	panic("x")
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	main := findMethod(prog, "main")
	if len(main.EH) != 1 {
		t.Fatalf("expected 1 EH clause (try/catch), got %d", len(main.EH))
	}
	for _, want := range []goir.Opcode{goir.OpLeave, goir.OpRethrow, goir.OpCallPanic, goir.OpCallRecover, goir.OpCallSetPanic} {
		if countOp(main, want) == 0 {
			t.Errorf("expected at least one %v in deferred function", want)
		}
	}
}

func TestLowerClosures(t *testing.T) {
	pkg := loadMain(t, `package main

func makeAdder(n int) func(int) int {
	return func(x int) int { return x + n }
}

func main() {
	add5 := makeAdder(5)
	println(add5(10))
}
`)
	bag := &diagnostics.Bag{}
	prog, ok := Lower(pkg, bag)
	if !ok {
		t.Fatalf("Lower failed: %v", bag.Items())
	}
	if findMethod(prog, "__closure_0") == nil {
		t.Error("expected a lifted closure method __closure_0")
	}
	if findMethod(prog, "__invoke") == nil {
		t.Error("expected the function-value dispatcher __invoke")
	}
	main := findMethod(prog, "main")
	// makeAdder's result type and add5 are func values (GoClosure).
	if countOp(main, goir.OpCallMethod) == 0 {
		t.Error("expected a call through the dispatcher")
	}
}

func TestLowerUnsupported(t *testing.T) {
	pkg := loadMain(t, `package main

func main() {
	go func(x int) { _ = x }(5)
}
`)
	bag := &diagnostics.Bag{}
	if _, ok := Lower(pkg, bag); ok {
		t.Fatal("Lower should reject features outside the M1 subset")
	}
	if !hasCode(bag, diagnostics.CodeUnsupportedFeature) {
		t.Errorf("expected GCLR0301, got %v", bag.Items())
	}
}

func hasCode(bag *diagnostics.Bag, code diagnostics.Code) bool {
	for _, d := range bag.Items() {
		if d.Code == code {
			return true
		}
	}
	return false
}
