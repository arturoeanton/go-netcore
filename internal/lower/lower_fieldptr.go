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
// supports a field reached through a pointer (&p.f), a package-level struct global
// (&g.f), or an addressable local struct held in a cell (&r.f). Returns false for
// shapes it does not handle (the caller falls back / reports an error).
func (l *funcLowerer) buildFieldAlias(sel *ast.SelectorExpr) bool {
	bt := l.exprType(sel.X)

	// container access: env-capture strategy + the struct type whose field we alias.
	switch {
	case bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KStruct:
		st := *bt.Elem
		fi := st.Struct.FieldIndex(sel.Sel.Name)
		if fi < 0 {
			return false
		}
		// container is the GoPtr value of sel.X; getter/setter go through GoPtr.Get/Set.
		l.emitFieldAliasViaPtr(func() { l.expr(sel.X) }, bt, st, fi)
		return true

	case bt.Kind == goir.KStruct:
		fi := bt.Struct.FieldIndex(sel.Sel.Name)
		if fi < 0 {
			return false
		}
		if gi, ok := l.globalRef(sel.X); ok {
			l.emitFieldAliasViaGlobal(gi, bt, fi)
			return true
		}
		if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
			// the cell is a GoPtr to the struct: same as the pointer case, capturing it.
			cidx := idx
			l.emitFieldAliasViaPtr(func() { l.emit(goir.Op{Code: goir.OpLdLoc, Local: cidx}) },
				goir.PtrType(elem), elem, elem.Struct.FieldIndex(sel.Sel.Name))
			return true
		}
		return false
	}
	return false
}

// emitFieldAliasViaPtr builds a field alias whose container is a GoPtr (emitPtr
// pushes it): the getter does *ptr.field, the setter writes it back through *ptr.
func (l *funcLowerer) emitFieldAliasViaPtr(emitPtr func(), ptrType, st goir.Type, fi int) {
	ft := st.Struct.Fields[fi].Type
	cap := []thunkCapture{{emit: emitPtr, typ: ptrType}}
	// getter: env[0] (GoPtr) -> Get -> unbox struct -> ldfld -> box
	l.buildAccessorClosure(cap, func(cl *funcLowerer) {
		cl.emitEnvArg(0, ptrType)
		cl.emit(goir.Op{Code: goir.OpPtrGet})
		cl.emitUnbox(st)
		cl.emit(goir.Op{Code: goir.OpLdFld, Struct: st.Struct, Field: fi})
		cl.emitBox(ft)
	})
	// setter: read *ptr into a temp, set the field from args[0], write *ptr back; ret null
	l.buildAccessorClosure(cap, func(cl *funcLowerer) {
		tmp := cl.addLocal(nil, st)
		cl.emitEnvArg(0, ptrType)
		cl.emit(goir.Op{Code: goir.OpPtrGet})
		cl.emitUnbox(st)
		cl.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(ft)
		cl.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})
		cl.emitEnvArg(0, ptrType)
		cl.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		cl.emitBox(st)
		cl.emit(goir.Op{Code: goir.OpPtrSet})
		cl.emit(goir.Op{Code: goir.OpLdNull})
	})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.fieldPtrExtern(st)})
}

// emitFieldAliasViaGlobal builds a field alias whose container is a struct global:
// getter reads global.field, setter writes it back through the global.
func (l *funcLowerer) emitFieldAliasViaGlobal(gi int, st goir.Type, fi int) {
	ft := st.Struct.Fields[fi].Type
	l.buildAccessorClosure(nil, func(cl *funcLowerer) {
		cl.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		cl.emit(goir.Op{Code: goir.OpLdFld, Struct: st.Struct, Field: fi})
		cl.emitBox(ft)
	})
	l.buildAccessorClosure(nil, func(cl *funcLowerer) {
		tmp := cl.addLocal(nil, st)
		cl.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		cl.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(ft)
		cl.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})
		cl.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		cl.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
		cl.emit(goir.Op{Code: goir.OpLdNull})
	})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.fieldPtrExtern(st)})
}
