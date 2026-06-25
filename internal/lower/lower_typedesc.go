package lower

import (
	"go/ast"
	"go/types"
	"regexp"
	"sort"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// types.TypeString renders the empty interface as the alias "any"; Go's reflect/%T always
// spells it "interface {}". Rewrite whole-word "any" occurrences (only the alias appears as a
// standalone token in a type string; identifiers like "Company"/"many" don't match).
var anyAliasRe = regexp.MustCompile(`\bany\b`)

// Runtime type descriptors. Each Go type observed at a reflect.TypeOf / ValueOf site
// — and, recursively, its element/key/field types — is given a stable id and a
// descriptor carrying its precise kind, name, type string, element/key types, and
// struct fields (name/tag/type/anonymous). The descriptors are registered with the
// runtime at startup (TypeReg), so reflect answers from real type information rather
// than inspecting a sample value: a slice's element type, a map's key type, a struct
// field's type and tag, and the exact width of a sized integer are all available
// even with no value in hand.

// reflect.Kind values — identical to Go's reflect package (and runtime GoKind).
const (
	rkInvalid = iota
	rkBool
	rkInt
	rkInt8
	rkInt16
	rkInt32
	rkInt64
	rkUint
	rkUint8
	rkUint16
	rkUint32
	rkUint64
	rkUintptr
	rkFloat32
	rkFloat64
	rkComplex64
	rkComplex128
	rkArray
	rkChan
	rkFunc
	rkInterface
	rkMap
	rkPtr
	rkSlice
	rkString
	rkStruct
	rkUnsafePointer
)

// typeDescEntry is a compile-time type descriptor queued for startup registration.
type typeDescEntry struct {
	id       int
	kind     int
	name     string
	pkgPath  string
	str      string
	elemId   int
	keyId    int
	arrayLen int
	fields   []typeFieldEntry
	methods  []string // method names in this type's method set (or an interface's requirements)
	// Dynamic-identity links: clrName is the emitted struct type name (for a struct
	// value reached through an interface) and namedId is the typed-box id (for an
	// identity-bearing named type); 0/"" when not applicable.
	clrName string
	namedId int64
}

type typeFieldEntry struct {
	name      string
	tag       string
	typeId    int
	anonymous bool
}

// buildAllTypeDescs builds a descriptor for every struct and named type defined in
// the program, so reflect over a value reached dynamically (through an interface)
// recovers precise type info — not just the types seen at a static reflect site.
// Types are visited in a deterministic order (canonical string) for reproducible
// builds.
func (c *lowerCtx) buildAllTypeDescs() {
	seen := map[*types.Named]bool{}
	var named []*types.Named
	add := func(n *types.Named) {
		if n != nil && !seen[n] {
			seen[n] = true
			named = append(named, n)
		}
	}
	for n := range c.structReg {
		add(n)
	}
	for n := range c.namedIds {
		add(n)
	}
	sort.Slice(named, func(i, j int) bool {
		return types.TypeString(named[i], nil) < types.TypeString(named[j], nil)
	})
	for _, n := range named {
		c.descId(n)
	}
}

// reflectKind maps a type's underlying type to its reflect.Kind.
func reflectKind(t types.Type) int {
	switch u := t.Underlying().(type) {
	case *types.Basic:
		switch u.Kind() {
		case types.Bool, types.UntypedBool:
			return rkBool
		case types.Int:
			return rkInt
		case types.Int8:
			return rkInt8
		case types.Int16:
			return rkInt16
		case types.Int32:
			return rkInt32
		case types.Int64:
			return rkInt64
		case types.Uint:
			return rkUint
		case types.Uint8:
			return rkUint8
		case types.Uint16:
			return rkUint16
		case types.Uint32:
			return rkUint32
		case types.Uint64:
			return rkUint64
		case types.Uintptr:
			return rkUintptr
		case types.Float32:
			return rkFloat32
		case types.Float64:
			return rkFloat64
		case types.Complex64:
			return rkComplex64
		case types.Complex128:
			return rkComplex128
		case types.String, types.UntypedString:
			return rkString
		case types.UnsafePointer:
			return rkUnsafePointer
		}
		return rkInvalid
	case *types.Slice:
		return rkSlice
	case *types.Array:
		return rkArray
	case *types.Map:
		return rkMap
	case *types.Pointer:
		return rkPtr
	case *types.Chan:
		return rkChan
	case *types.Signature:
		return rkFunc
	case *types.Struct:
		return rkStruct
	case *types.Interface:
		return rkInterface
	}
	return rkInvalid
}

// typeDescStr renders a type the way reflect.Type.String does: package-qualified by
// name ("main.User"), with composites spelled out ("[]string", "map[string]int").
func typeDescStr(t types.Type) string {
	// Go reflect renders a generic instantiation with no space after commas in the
	// type-argument list ("main.Pair[string,int]"); types.TypeString inserts ", ".
	if s, ok := reflectGenericName(t); ok {
		return s
	}
	s := types.TypeString(t, func(p *types.Package) string { return p.Name() })
	return anyAliasRe.ReplaceAllString(s, "interface {}")
}

// reflectGenericName renders a type that contains a generic instantiation the way Go
// reflect does — no space after commas inside the [...] type-argument list. Returns
// ok=false for types with no generic instantiation, so the caller uses types.TypeString.
func reflectGenericName(t types.Type) (string, bool) {
	switch u := t.(type) {
	case *types.Named:
		ta := u.TypeArgs()
		if ta == nil || ta.Len() == 0 {
			return "", false
		}
		base := u.Obj().Name()
		if p := u.Obj().Pkg(); p != nil {
			base = p.Name() + "." + base
		}
		parts := make([]string, ta.Len())
		for i := 0; i < ta.Len(); i++ {
			parts[i] = typeDescStr(ta.At(i))
		}
		return base + "[" + strings.Join(parts, ",") + "]", true
	case *types.Slice:
		if s, ok := reflectGenericName(u.Elem()); ok {
			return "[]" + s, true
		}
	case *types.Pointer:
		if s, ok := reflectGenericName(u.Elem()); ok {
			return "*" + s, true
		}
	case *types.Map:
		ks, kok := reflectGenericName(u.Key())
		vs, vok := reflectGenericName(u.Elem())
		if kok || vok {
			if !kok {
				ks = typeDescStr(u.Key())
			}
			if !vok {
				vs = typeDescStr(u.Elem())
			}
			return "map[" + ks + "]" + vs, true
		}
	}
	return "", false
}

// descId returns the runtime descriptor id for t, building it and (recursively) its
// element/key/field types on first sight. Types are deduplicated by canonical
// string, and the id is reserved before recursing so recursive types terminate.
func (c *lowerCtx) descId(t types.Type) int {
	key := types.TypeString(t, nil)
	if id, ok := c.typeDescIds[key]; ok {
		return id
	}
	id := len(c.typeDescs)
	c.typeDescIds[key] = id
	entry := typeDescEntry{id: id, kind: reflectKind(t), str: typeDescStr(t), elemId: -1, keyId: -1}
	// A predeclared basic type has a name in Go reflect (reflect.TypeOf("").Name() ==
	// "string"); set it so a struct field of basic type reports its type name.
	if b, ok := t.(*types.Basic); ok && b.Info()&types.IsUntyped == 0 {
		entry.name = b.Name()
	}
	if named, ok := t.(*types.Named); ok {
		entry.name = named.Obj().Name()
		if named.Obj().Pkg() != nil {
			entry.pkgPath = named.Obj().Pkg().Path()
		}
		// Link the typed-box id so reflect on a dynamic (interface) value of an
		// identity-bearing named type recovers this descriptor.
		if nid, ok := c.namedIdentity(named); ok {
			entry.namedId = nid
		}
	}
	// Link a struct's emitted CLR type name so reflect on a dynamic struct value
	// (boxed into an interface) recovers this descriptor from value.GetType().
	if gt, ok := c.goType(t); ok && gt.Kind == goir.KStruct && gt.Struct != nil {
		entry.clrName = gt.Struct.Name
	}
	// Method set: an interface's required methods, or a concrete type's value-receiver
	// method set (a *T descriptor, built separately, gets *T's full method set). Names
	// drive reflect's NumMethod/Method and Implements/AssignableTo.
	if iface, ok := t.Underlying().(*types.Interface); ok {
		for i := 0; i < iface.NumMethods(); i++ {
			entry.methods = append(entry.methods, iface.Method(i).Name())
		}
	} else {
		ms := types.NewMethodSet(t)
		for i := 0; i < ms.Len(); i++ {
			entry.methods = append(entry.methods, ms.At(i).Obj().Name())
		}
	}
	c.typeDescs = append(c.typeDescs, entry)

	switch u := t.Underlying().(type) {
	case *types.Slice:
		c.typeDescs[id].elemId = c.descId(u.Elem())
	case *types.Array:
		c.typeDescs[id].elemId = c.descId(u.Elem())
		c.typeDescs[id].arrayLen = int(u.Len())
	case *types.Pointer:
		c.typeDescs[id].elemId = c.descId(u.Elem())
	case *types.Chan:
		c.typeDescs[id].elemId = c.descId(u.Elem())
	case *types.Map:
		c.typeDescs[id].keyId = c.descId(u.Key())
		c.typeDescs[id].elemId = c.descId(u.Elem())
	case *types.Struct:
		fields := make([]typeFieldEntry, 0, u.NumFields())
		for i := 0; i < u.NumFields(); i++ {
			f := u.Field(i)
			fields = append(fields, typeFieldEntry{
				name:      f.Name(),
				tag:       u.Tag(i),
				typeId:    c.descId(f.Type()),
				anonymous: f.Embedded(),
			})
		}
		c.typeDescs[id].fields = fields
	}
	return id
}

// emitOneTypeDesc appends the startup calls registering one type descriptor with
// the runtime (TypeReg.RegisterType + a RegisterField per struct field). Emitted
// per-entry so the caller can split the init across chunks (the 64 KiB IL limit).
func (l *funcLowerer) emitOneTypeDesc(e typeDescEntry) {
	regType := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "TypeReg", Method: "RegisterType",
		Params: []goir.Type{goir.TInt32, goir.TInt32, goir.TString, goir.TString, goir.TString, goir.TInt32, goir.TInt32, goir.TInt32},
		Ret:    goir.TVoid,
	}
	regField := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "TypeReg", Method: "RegisterField",
		Params: []goir.Type{goir.TInt32, goir.TString, goir.TString, goir.TInt32, goir.TBool},
		Ret:    goir.TVoid,
	}
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.id)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.kind)})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: e.name})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: e.pkgPath})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: e.str})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.elemId)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.keyId)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.arrayLen)})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: regType})
	for _, f := range e.fields {
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.id)})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: f.name})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: f.tag})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(f.typeId)})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: boolToInt(f.anonymous)})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: regField})
	}
	for _, mn := range e.methods {
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.id)})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: mn})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "TypeReg", Method: "RegisterMethod",
			Params: []goir.Type{goir.TInt32, goir.TString}, Ret: goir.TVoid,
		}})
	}
	if e.clrName != "" {
		l.emit(goir.Op{Code: goir.OpStrConst, Str: e.clrName})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.id)})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "TypeReg", Method: "LinkClr",
			Params: []goir.Type{goir.TString, goir.TInt32}, Ret: goir.TVoid,
		}})
	}
	if e.namedId != 0 {
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: e.namedId})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(e.id)})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "TypeReg", Method: "LinkNamed",
			Params: []goir.Type{goir.TInt64, goir.TInt32}, Ret: goir.TVoid,
		}})
	}
}

func boolToInt(b bool) int64 {
	if b {
		return 1
	}
	return 0
}

// reflectOfCall lowers reflect.TypeOf(x) / reflect.ValueOf(x): box the argument and
// pass the descriptor id of its static type, so the resulting reflect.Type/Value
// carries precise type information. When the static type is an interface the dynamic
// type is what matters, so -1 is passed and the runtime falls back to the value's
// own identity.
func (l *funcLowerer) reflectOfCall(e *ast.CallExpr, name string) goir.Type {
	arg := e.Args[0]
	at := l.pkg.TypesInfo.TypeOf(arg)
	l.exprCoerced(arg, goir.TObject)
	descID := -1
	if _, isIface := at.Underlying().(*types.Interface); !isIface {
		descID = l.descId(at)
	}
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(descID)})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Reflect", Method: name,
		Params: []goir.Type{goir.TObject, goir.TInt32}, Ret: goir.TObject,
	}})
	return goir.TObject
}

// reflectTypeForCall lowers reflect.TypeFor[T](): pass the descriptor id of the
// explicit (or inferred) type argument T, so the resulting reflect.Type is precise.
func (l *funcLowerer) reflectTypeForCall(e *ast.CallExpr) goir.Type {
	var T types.Type
	switch fn := unparen(e.Fun).(type) {
	case *ast.IndexExpr:
		T = l.pkg.TypesInfo.TypeOf(fn.Index)
	case *ast.IndexListExpr:
		if len(fn.Indices) == 1 {
			T = l.pkg.TypesInfo.TypeOf(fn.Indices[0])
		}
	}
	descID := -1
	if T != nil {
		if _, isIface := T.Underlying().(*types.Interface); !isIface {
			descID = l.descId(T)
		}
	}
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(descID)})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Reflect", Method: "TypeFor",
		Params: []goir.Type{goir.TInt32}, Ret: goir.TObject,
	}})
	return goir.TObject
}
