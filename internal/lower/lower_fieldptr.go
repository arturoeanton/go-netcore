package lower

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// Addressable struct fields. Structs are boxed value types, so a field has no
// independent storage a pointer could alias. &s.field instead builds a field-alias
// GoPtr carrying a getter/setter pair that re-navigate the field's stable container
// on each *p access (Rt.FieldPtr). Re-navigating rather than caching a copy is what
// keeps sync/atomic on a struct field correct (under the atomic shim's lock).

// buildAccessorClosure builds a lifted (env, args) -> object closure whose body
// emitBody leaves the boxed result on the stack; it then returns. Captures are
// evaluated now into the env. Leaves a GoClosure on the stack. Mirrors buildThunk
// but yields a value instead of null.
func (l *funcLowerer) buildAccessorClosure(captures []thunkCapture, emitBody func(cl *funcLowerer)) {
	l.needsInvoker = true
	l.invokeMethod()
	id := len(l.closures)
	method := &goir.Method{
		Name:    "__facc_" + itoa(id),
		GoName:  "__facc_" + itoa(id),
		Params:  []goir.Type{goir.TObjectArray, goir.TObjectArray}, // env, args
		Ret:     goir.TObject,
		Results: []goir.Type{goir.TObject},
	}
	l.closures = append(l.closures, &closureInfo{id: id, method: method})
	l.prog.Methods = append(l.prog.Methods, method)

	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(id)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(captures))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, c := range captures {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		c.emit()
		l.emitBox(c.typ)
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
	l.emit(goir.Op{Code: goir.OpClosNew})

	cl := &funcLowerer{lowerCtx: l.lowerCtx, m: method, ok: true, inClosure: true}
	cl.typeSubst = l.typeSubst
	cl.locals = map[types.Object]int{}
	cl.cells = map[int]goir.Type{}
	emitBody(cl)
	cl.emit(goir.Op{Code: goir.OpRet})
	if !cl.ok {
		l.ok = false
	}
}

// fieldPtrExtern is the Rt.FieldPtr(getter, setter) call that wraps a getter/setter
// closure pair into a field-alias GoPtr.
func (l *funcLowerer) fieldPtrExtern(elem goir.Type) *goir.Extern {
	return &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "FieldPtr",
		Params: []goir.Type{goir.TFunc, goir.TFunc}, Ret: goir.PtrType(elem),
	}
}

// buildFieldAlias lowers &s.field, leaving a field-alias GoPtr on the stack. It
// supports a field (direct or promoted through value embeds) reached through a
// pointer (&p.f), a package-level struct global (&g.f), or an addressable local
// struct held in a cell (&r.f). Returns false for shapes it does not handle.
func (l *funcLowerer) buildFieldAlias(sel *ast.SelectorExpr) bool {
	bt := l.exprType(sel.X)
	var root goir.Type
	switch {
	case bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KStruct:
		root = *bt.Elem
	case bt.Kind == goir.KStruct:
		root = bt
	default:
		return false
	}

	// Resolve the (possibly promoted) field path and the final field type, requiring
	// value-embed-only navigation (a pointer embed has no in-place field storage).
	path := []int{root.Struct.FieldIndex(sel.Sel.Name)}
	if path[0] < 0 {
		p, ok := l.promotedFieldPath(sel)
		if !ok {
			return false
		}
		path = p
	}
	cont := root
	for _, idx := range path[:len(path)-1] {
		ft := cont.Struct.Fields[idx].Type
		if ft.Kind != goir.KStruct {
			return false
		}
		cont = ft
	}
	ft := cont.Struct.Fields[path[len(path)-1]].Type

	switch {
	case bt.Kind == goir.KPtr:
		l.emitFieldAliasPtr(func() { l.expr(sel.X) }, bt, root, path, ft)
		return true
	default:
		if gi, ok := l.globalRef(sel.X); ok {
			l.emitFieldAliasGlobal(gi, root, path, ft)
			return true
		}
		if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
			cidx := idx
			l.emitFieldAliasPtr(func() { l.emit(goir.Op{Code: goir.OpLdLoc, Local: cidx}) },
				goir.PtrType(elem), elem, path, ft)
			return true
		}
		return false
	}
}

// emitLdfldaChain, given the address of a root struct on the stack, emits a ldflda
// chain through path[:last] (value embeds) and returns the struct that directly
// holds the final field.
func (l *funcLowerer) emitLdfldaChain(root goir.Type, path []int) goir.Type {
	cont := root
	for _, idx := range path[:len(path)-1] {
		l.emit(goir.Op{Code: goir.OpLdFldA, Struct: cont.Struct, Field: idx})
		cont = cont.Struct.Fields[idx].Type
	}
	return cont
}

// emitFieldAliasPtr builds a field alias whose container is a GoPtr (emitPtr pushes
// it): the getter reads *ptr then navigates to the field; the setter navigates and
// writes the field, then writes *ptr back.
func (l *funcLowerer) emitFieldAliasPtr(emitPtr func(), ptrType, root goir.Type, path []int, ft goir.Type) {
	cap := []thunkCapture{{emit: emitPtr, typ: ptrType}}
	last := path[len(path)-1]
	l.buildAccessorClosure(cap, func(cl *funcLowerer) {
		cl.emitEnvArg(0, ptrType)
		cl.emit(goir.Op{Code: goir.OpPtrGet})
		cl.emitUnbox(root)
		cl.emitFieldChain(root, path)
		cl.emitBox(ft)
	})
	l.buildAccessorClosure(cap, func(cl *funcLowerer) {
		tmp := cl.addLocal(nil, root)
		cl.emitEnvArg(0, ptrType)
		cl.emit(goir.Op{Code: goir.OpPtrGet})
		cl.emitUnbox(root)
		cl.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		cont := cl.emitLdfldaChain(root, path)
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(ft)
		cl.emit(goir.Op{Code: goir.OpStFld, Struct: cont.Struct, Field: last})
		cl.emitEnvArg(0, ptrType)
		cl.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		cl.emitBox(root)
		cl.emit(goir.Op{Code: goir.OpPtrSet})
		cl.emit(goir.Op{Code: goir.OpLdNull})
	})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.fieldPtrExtern(ft)})
}

// emitFieldAliasGlobal builds a field alias whose container is a struct global.
func (l *funcLowerer) emitFieldAliasGlobal(gi int, root goir.Type, path []int, ft goir.Type) {
	last := path[len(path)-1]
	l.buildAccessorClosure(nil, func(cl *funcLowerer) {
		cl.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		cl.emitFieldChain(root, path)
		cl.emitBox(ft)
	})
	l.buildAccessorClosure(nil, func(cl *funcLowerer) {
		tmp := cl.addLocal(nil, root)
		cl.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		cl.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		cont := cl.emitLdfldaChain(root, path)
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(ft)
		cl.emit(goir.Op{Code: goir.OpStFld, Struct: cont.Struct, Field: last})
		cl.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
		cl.emit(goir.Op{Code: goir.OpLdNull})
	})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.fieldPtrExtern(ft)})
}
