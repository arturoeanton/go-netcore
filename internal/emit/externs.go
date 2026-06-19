package emit

import "github.com/arturoeanton/go-netcore/internal/goir"

// Fixed table sizes the dynamic shim references are appended after.
const (
	fixedTypeRefs    = 29
	fixedMemberRefs  = 60
	fixedAssemblyRef = 2
)

// externType is a distinct shim type referenced by the program.
type externType struct {
	asmRow          int
	name, namespace string
}

// externCollection holds the program's distinct shim assemblies, types and
// methods, in row order, with the MemberRef token assigned to each method.
type externCollection struct {
	assemblies []string
	asmRowOf   map[string]int
	types      []externType
	typeRowOf  map[string]int
	methods    []*goir.Extern
	tokOf      map[string]uint32
}

// collectExterns scans every method body for OpCallExtern and assigns
// AssemblyRef (row 3+), TypeRef (row 30+) and MemberRef (row 61+) rows + tokens
// to each distinct shim reference, in first-seen order.
func collectExterns(prog *goir.Program) *externCollection {
	ec := &externCollection{
		asmRowOf:  map[string]int{},
		typeRowOf: map[string]int{},
		tokOf:     map[string]uint32{},
	}
	addAsm := func(a string) int {
		if r, ok := ec.asmRowOf[a]; ok {
			return r
		}
		r := fixedAssemblyRef + 1 + len(ec.assemblies)
		ec.assemblies = append(ec.assemblies, a)
		ec.asmRowOf[a] = r
		return r
	}
	addType := func(e *goir.Extern) {
		if _, ok := ec.typeRowOf[e.TypeKey()]; ok {
			return
		}
		ec.typeRowOf[e.TypeKey()] = fixedTypeRefs + 1 + len(ec.types)
		ec.types = append(ec.types, externType{asmRow: addAsm(e.Assembly), name: e.Type, namespace: e.Namespace})
	}
	for _, m := range prog.Methods {
		for _, op := range m.Code {
			if op.Code != goir.OpCallExtern || op.Extern == nil {
				continue
			}
			e := op.Extern
			if _, ok := ec.tokOf[e.Key()]; ok {
				continue
			}
			addType(e)
			row := fixedMemberRefs + 1 + len(ec.methods)
			ec.methods = append(ec.methods, e)
			ec.tokOf[e.Key()] = 0x0A000000 | uint32(row)
		}
	}
	return ec
}

// token returns the MemberRef token for an extern method.
func (ec *externCollection) token(e *goir.Extern) uint32 { return ec.tokOf[e.Key()] }
