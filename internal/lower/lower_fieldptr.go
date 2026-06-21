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

// fieldPtrExtern is the Rt.FieldPtr(getter, setter, typeId) call that wraps a
// getter/setter closure pair into a field-alias GoPtr. typeId tags the pointee's
// struct type so a *Struct field alias (&s.field) supports type assertion and
// pointer-receiver interface dispatch like any other *Struct pointer.
func (l *funcLowerer) fieldPtrExtern(elem goir.Type) *goir.Extern {
	return &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "FieldPtr",
		Params: []goir.Type{goir.TFunc, goir.TFunc, goir.TInt64}, Ret: goir.PtrType(elem),
	}
}

// emitFieldPtrCall pushes the pointee's struct type id (0 for non-struct fields)
// and calls Rt.FieldPtr, after the getter/setter closures are already on the stack.
func (l *funcLowerer) emitFieldPtrCall(ft goir.Type) {
	var id int64
	if ft.Kind == goir.KStruct {
		id = int64(ft.Struct.Id)
	}
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.fieldPtrExtern(ft)})
}

// buildFieldAlias lowers &s.field (possibly a multi-level chain s.a.b.f), leaving a
// field-alias GoPtr on the stack. It walks the selector chain to its root — a
// pointer (&p.…f), a package-level struct global (&g.…f), or an addressable local
// struct held in a cell (&r.…f) — and aliases the field reached by the field path
// from there. Returns false for shapes it does not handle.
func (l *funcLowerer) buildFieldAlias(sel *ast.SelectorExpr) bool {
	var names []string
	innermost := sel // the selector whose .Sel is the final field (for promoted leaf)
	cur := ast.Expr(sel)
	for {
		s, ok := unparen(cur).(*ast.SelectorExpr)
		if !ok {
			return false
		}
		names = append([]string{s.Sel.Name}, names...)
		bt := l.exprType(s.X)
		if bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KStruct {
			root := *bt.Elem
			path, ft, ok := l.fieldPath(root, names, innermost)
			if !ok {
				return false
			}
			sx := s.X
			l.emitFieldAliasPtr(func() { l.expr(sx) }, bt, root, path, ft)
			return true
		}
		if bt.Kind != goir.KStruct {
			return false
		}
		if gi, ok := l.globalRef(s.X); ok {
			path, ft, ok := l.fieldPath(bt, names, innermost)
			if !ok {
				return false
			}
			l.emitFieldAliasGlobal(gi, bt, path, ft)
			return true
		}
		if idx, elem, isCell := l.identCell(s.X); isCell && elem.Kind == goir.KStruct {
			path, ft, ok := l.fieldPath(elem, names, innermost)
			if !ok {
				return false
			}
			cidx := idx
			l.emitFieldAliasPtr(func() { l.emit(goir.Op{Code: goir.OpLdLoc, Local: cidx}) },
				goir.PtrType(elem), elem, path, ft)
			return true
		}
		// &slice[i].field: alias the field through a pointer to the slice element
		// (ElemAddr aliases the backing array, so writes are observed by the slice).
		if ix, ok := unparen(s.X).(*ast.IndexExpr); ok {
			xt := l.exprType(ix.X)
			if xt.Kind == goir.KSlice && xt.Elem != nil && xt.Elem.Kind == goir.KStruct {
				elem := *xt.Elem
				path, ft, ok := l.fieldPath(elem, names, innermost)
				if !ok {
					return false
				}
				ixX, ixIndex := ix.X, ix.Index
				emitElemPtr := func() {
					l.expr(ixX)
					l.expr(ixIndex)
					l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
						Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ElemAddr",
						Params: []goir.Type{xt, goir.TInt64}, Ret: goir.PtrType(elem),
					}})
				}
				l.emitFieldAliasPtr(emitElemPtr, goir.PtrType(elem), elem, path, ft)
				return true
			}
		}
		cur = s.X // keep walking up the chain
	}
}

// fieldPath resolves a chain of field names (top-down from root) to a field-index
// path and the final field's type. Intermediate names must be direct value-struct
// fields; the single-name case may be a promoted field (value embeds only).
func (l *funcLowerer) fieldPath(root goir.Type, names []string, leaf *ast.SelectorExpr) ([]int, goir.Type, bool) {
	if len(names) == 1 {
		if fi := root.Struct.FieldIndex(names[0]); fi >= 0 {
			return []int{fi}, root.Struct.Fields[fi].Type, true
		}
		p, ok := l.promotedFieldPath(leaf)
		if !ok {
			return nil, goir.Type{}, false
		}
		cont := root
		for _, idx := range p[:len(p)-1] {
			if cont.Struct.Fields[idx].Type.Kind != goir.KStruct {
				return nil, goir.Type{}, false
			}
			cont = cont.Struct.Fields[idx].Type
		}
		return p, cont.Struct.Fields[p[len(p)-1]].Type, true
	}
	path := make([]int, 0, len(names))
	cur := root
	for i, name := range names {
		if cur.Kind != goir.KStruct {
			return nil, goir.Type{}, false
		}
		fi := cur.Struct.FieldIndex(name)
		if fi < 0 {
			return nil, goir.Type{}, false // promoted mid-chain unsupported
		}
		path = append(path, fi)
		if i == len(names)-1 {
			return path, cur.Struct.Fields[fi].Type, true
		}
		cur = cur.Struct.Fields[fi].Type
	}
	return nil, goir.Type{}, false
}

// emitGlobalAlias builds a pointer aliasing a package-level variable directly
// (&global): the getter reads it via ldsfld and the setter writes it via stsfld, so
// a pointer-receiver method called on a global observes and mutates the live value.
func (l *funcLowerer) emitGlobalAlias(gi int, t goir.Type) {
	l.buildAccessorClosure(nil, func(cl *funcLowerer) {
		cl.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		cl.emitBox(t)
	})
	l.buildAccessorClosure(nil, func(cl *funcLowerer) {
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(t)
		cl.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
		cl.emit(goir.Op{Code: goir.OpLdNull})
	})
	l.emitFieldPtrCall(t)
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
	l.emitFieldPtrCall(ft)
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
	l.emitFieldPtrCall(ft)
}
