package lower

import (
	"go/ast"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// analyzeAddrTaken finds local variables that must be stored as GoPtr cells: ones
// whose address is taken via &ident, ones implicitly addressed by a
// pointer-receiver method call, and ones captured by a nested function literal.
func (c *lowerCtx) analyzeAddrTaken(body ast.Node) map[types.Object]bool {
	pkg := c.pkg
	set := map[types.Object]bool{}
	if body == nil {
		return set
	}
	mark := func(e ast.Expr) {
		e = unparen(e)
		// &x.f (and deeper, &x.f.g): the field has no standalone storage, so the
		// root value local must become a cell to be aliased. Walk to the root,
		// stopping if a base is a pointer (then &p.f aliases through the pointer and
		// the base needs no cell).
		for {
			sel, ok := e.(*ast.SelectorExpr)
			if !ok {
				break
			}
			if isPointerType(pkg.TypesInfo.TypeOf(sel.X)) {
				return
			}
			e = unparen(sel.X)
		}
		if id, ok := e.(*ast.Ident); ok {
			if v, ok := pkg.TypesInfo.Uses[id].(*types.Var); ok {
				// Opaque value-type shims are already reference handles; taking
				// their address does not require a cell.
				if t, ok := c.goType(v.Type()); ok && t.Shim != "" {
					return
				}
				set[v] = true
			}
		}
	}
	ast.Inspect(body, func(n ast.Node) bool {
		switch n := n.(type) {
		case *ast.FuncLit:
			// Captured variables become shared cells. Do not descend: the literal
			// body is analyzed on its own when lowered.
			for _, v := range c.litFreeVars(n) {
				set[v] = true
			}
			return false
		case *ast.UnaryExpr:
			if n.Op == token.AND {
				mark(n.X)
			}
		case *ast.SelectorExpr:
			// A pointer-receiver method called on, or taken as a value from, an
			// addressable value takes its address implicitly (u.PtrMethod() and the
			// method value u.PtrMethod both bind &u).
			if seln := pkg.TypesInfo.Selections[n]; seln != nil && seln.Kind() == types.MethodVal {
				if fn, ok := seln.Obj().(*types.Func); ok {
					if sig, ok := fn.Type().(*types.Signature); ok && isPointerType(sig.Recv().Type()) {
						if !isPointerType(pkg.TypesInfo.TypeOf(n.X)) {
							mark(n.X)
						}
					}
				}
			}
		}
		return true
	})
	return set
}

func isPointerType(t types.Type) bool {
	_, ok := t.Underlying().(*types.Pointer)
	return ok
}

func unparen(e ast.Expr) ast.Expr {
	for {
		p, ok := e.(*ast.ParenExpr)
		if !ok {
			return e
		}
		e = p.X
	}
}

// ptrNew emits OpPtrNew, tagging the cell with the pointee's named struct type id
// (0 for non-struct pointees) so pointer-receiver interface dispatch can match it.
func (l *funcLowerer) ptrNew(pointee goir.Type) {
	id := 0
	if pointee.Kind == goir.KStruct {
		id = pointee.Struct.Id
	}
	l.emit(goir.Op{Code: goir.OpPtrNew, Int: int64(id)})
}

// declareLocal creates a local for obj. Address-taken locals become GoPtr cells
// (CLR type *T); cells[idx] records the logical pointee type.
func (l *funcLowerer) declareLocal(obj types.Object, t goir.Type) (idx int, isCell bool) {
	if obj != nil && l.addrTaken[obj] {
		idx = len(l.m.Locals)
		l.m.Locals = append(l.m.Locals, goir.PtrType(t))
		l.cells[idx] = t
		l.locals[obj] = idx
		return idx, true
	}
	return l.addLocal(obj, t), false
}

// loadVar pushes the value of a local, dereferencing the cell when needed.
func (l *funcLowerer) loadVar(idx int) {
	if elem, isCell := l.cells[idx]; isCell {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(elem)
		return
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
}

// initLocal initializes a freshly-declared local; emitValue pushes the unboxed
// value. For a cell it allocates the GoPtr.
func (l *funcLowerer) initLocal(idx int, emitValue func()) {
	if elem, isCell := l.cells[idx]; isCell {
		emitValue()
		l.emitBox(elem)
		l.ptrNew(elem)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: idx})
		return
	}
	emitValue()
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idx})
}

// assignLocal updates an existing local; emitValue pushes the unboxed value.
func (l *funcLowerer) assignLocal(idx int, emitValue func()) {
	if elem, isCell := l.cells[idx]; isCell {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
		emitValue()
		l.emitBox(elem)
		l.emit(goir.Op{Code: goir.OpPtrSet})
		return
	}
	emitValue()
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idx})
}

// identCell reports whether e is an identifier whose local is a GoPtr cell,
// returning the cell's local index and pointee (logical) type.
func (l *funcLowerer) identCell(e ast.Expr) (idx int, elem goir.Type, ok bool) {
	id, isID := unparen(e).(*ast.Ident)
	if !isID {
		return 0, goir.Type{}, false
	}
	idx, found := l.locals[l.pkg.TypesInfo.ObjectOf(id)]
	if !found {
		return 0, goir.Type{}, false
	}
	elem, isCell := l.cells[idx]
	return idx, elem, isCell
}

// emitAddr pushes the GoPtr cell of an addressable identifier.
func (l *funcLowerer) emitAddr(e ast.Expr) bool {
	id, ok := unparen(e).(*ast.Ident)
	if !ok {
		return false
	}
	idx, ok := l.locals[l.pkg.TypesInfo.ObjectOf(id)]
	if !ok {
		return false
	}
	if _, isCell := l.cells[idx]; !isCell {
		return false
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx}) // the cell IS the pointer
	return true
}

// emitAddressable leaves a GoPtr aliasing e on the stack, for any addressable
// form (an address-taken local cell, a struct field reached directly or through a
// pointer, a package global, or a slice element). Reports false if e cannot be
// addressed. It is the bool-returning core that addrOf and method-value receivers
// share.
func (l *funcLowerer) emitAddressable(e ast.Expr) bool {
	switch x := unparen(e).(type) {
	case *ast.Ident:
		if l.emitAddr(x) {
			return true
		}
		if gi, ok := l.globalRef(x); ok {
			l.emitGlobalAlias(gi, l.exprType(x))
			return true
		}
		return false
	case *ast.SelectorExpr:
		return l.buildFieldAlias(x)
	case *ast.IndexExpr:
		xt := l.exprType(x.X)
		if xt.Kind != goir.KSlice {
			return false
		}
		l.expr(x.X)
		l.expr(x.Index)
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ElemAddr",
			Params: []goir.Type{xt, goir.TInt64}, Ret: goir.PtrType(*xt.Elem),
		}})
		return true
	}
	return false
}

// addrOf lowers &operand, leaving a GoPtr on the stack.
func (l *funcLowerer) addrOf(e *ast.UnaryExpr) {
	// &opaqueShimValue is the same runtime object (it is already a reference).
	if l.exprType(e.X).Shim != "" {
		l.expr(e.X)
		return
	}
	switch x := unparen(e.X).(type) {
	case *ast.Ident:
		if l.emitAddr(x) {
			return
		}
		// &globalVar: a pointer aliasing the package-level variable (ldsfld/stsfld).
		if gi, ok := l.globalRef(x); ok {
			l.emitGlobalAlias(gi, l.exprType(x))
			return
		}
		l.fail(e.Pos(), "address of "+x.Name)
	case *ast.SelectorExpr:
		// &s.field: a struct field has no standalone storage, so build a field-alias
		// pointer that reads/writes the field through its stable container.
		if l.buildFieldAlias(x) {
			return
		}
		l.fail(e.Pos(), "address-of field "+x.Sel.Name)
	case *ast.CompositeLit:
		// &T{...}: a fresh cell holding the composite value.
		t := l.compositeLit(x)
		l.emitBox(t)
		l.ptrNew(t)
	case *ast.IndexExpr:
		// &s[i] on a slice (or slice-backed array): a pointer aliasing the
		// element's slot in the backing array, so the slice and the pointer
		// observe the same storage.
		xt := l.exprType(x.X)
		if xt.Kind != goir.KSlice {
			l.fail(e.Pos(), "address-of element (only slice elements are supported)")
			return
		}
		l.expr(x.X)
		l.expr(x.Index)
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ElemAddr",
			Params: []goir.Type{xt, goir.TInt64}, Ret: goir.PtrType(*xt.Elem),
		}})
	default:
		l.fail(e.Pos(), "address-of (only &variable and &T{...} are supported)")
	}
}

// derefRead lowers *p (read), leaving the pointee value.
func (l *funcLowerer) derefRead(e *ast.StarExpr) {
	pt := l.exprType(e.X)
	// *opaqueShimPtr: *url.URL and url.URL are the same opaque object, so a deref
	// produces a value copy — clone it (when a cloner is registered) to preserve
	// Go value semantics (u := *p; u.field = x must not mutate *p).
	if pt.Kind == goir.KObject && pt.Shim != "" {
		l.expr(e.X)
		if cf, ok := opaqueShimClone[pt.Shim]; ok {
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: cf.csType, Method: cf.csMethod,
				Params: []goir.Type{goir.TObject}, Ret: pt,
			}})
		}
		return
	}
	if pt.Kind != goir.KPtr {
		l.fail(e.Pos(), "dereference of non-pointer")
		return
	}
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(*pt.Elem)
}

// derefWrite lowers *p = v.
func (l *funcLowerer) derefWrite(e *ast.StarExpr, rhs ast.Expr) {
	pt := l.exprType(e.X)
	if pt.Kind != goir.KPtr {
		l.fail(e.Pos(), "dereference of non-pointer")
		return
	}
	l.expr(e.X)
	l.expr(rhs)
	l.emitBox(*pt.Elem)
	l.emit(goir.Op{Code: goir.OpPtrSet})
}

// newCall lowers new(T): a fresh cell holding the zero value of T.
func (l *funcLowerer) newCall(e *ast.CallExpr) goir.Type {
	t, ok := l.goType(l.pkg.TypesInfo.TypeOf(e.Args[0]))
	if !ok {
		l.fail(e.Pos(), "new(T) type")
		return goir.TVoid
	}
	// For an opaque value-type shim, *T and T share one runtime handle, so
	// new(T) just produces a fresh opaque object (no GoPtr cell).
	if t.Kind == goir.KObject && t.Shim != "" {
		l.emitZeroValue(t)
		return t
	}
	l.emitBoxedZero(t)
	l.ptrNew(t)
	return goir.PtrType(t)
}

// ptrStructFieldRead lowers p.f where p is *struct (auto-dereference).
func (l *funcLowerer) ptrStructFieldRead(e *ast.SelectorExpr, pt goir.Type) {
	st := *pt.Elem
	fi := st.Struct.FieldIndex(e.Sel.Name)
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(st)
	if fi >= 0 {
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: st.Struct, Field: fi})
		return
	}
	// Promoted field through embedded fields (p.f where f is in an embed).
	if path, ok := l.promotedFieldPath(e); ok {
		l.emitFieldChain(st, path)
		return
	}
	l.fail(e.Pos(), "unknown field "+e.Sel.Name)
}

// ptrStructFieldWrite lowers p.f = v where p is *struct: unbox the struct from
// the cell, mutate the field, and store it back.
func (l *funcLowerer) ptrStructFieldWrite(e *ast.SelectorExpr, pt goir.Type, rhs ast.Expr) {
	st := *pt.Elem
	fi := st.Struct.FieldIndex(e.Sel.Name)
	var path []int
	if fi < 0 {
		// Promoted field through embedded fields (p.f where f is in an embed).
		p, ok := l.promotedFieldPath(e)
		if !ok {
			l.fail(e.Pos(), "unknown field "+e.Sel.Name)
			return
		}
		path = p
	}
	pTmp := l.addLocal(nil, pt)
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: pTmp})

	sTmp := l.addLocal(nil, st)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(st)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: sTmp})

	l.emit(goir.Op{Code: goir.OpLdLocA, Local: sTmp})
	cur := st
	if path != nil {
		// Navigate value embeds to the struct that directly holds the field.
		for _, idx := range path[:len(path)-1] {
			ft := cur.Struct.Fields[idx].Type
			if ft.Kind != goir.KStruct {
				l.fail(e.Pos(), "promoted field write through a pointer embed")
				return
			}
			l.emit(goir.Op{Code: goir.OpLdFldA, Struct: cur.Struct, Field: idx})
			cur = ft
		}
		fi = path[len(path)-1]
	}
	l.expr(rhs)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: cur.Struct, Field: fi})

	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sTmp})
	l.emitBox(st)
	l.emit(goir.Op{Code: goir.OpPtrSet})
}

// emitEmbeddedIfaceValue leaves on the stack the embedded interface value reached
// from a concrete receiver through embedPath (struct { SomeIface }), so a method
// promoted from that interface can be dispatched on it.
func (l *funcLowerer) emitEmbeddedIfaceValue(sel *ast.SelectorExpr, embedPath []int) {
	bt := l.exprType(sel.X)
	l.expr(sel.X)
	if bt.Kind == goir.KPtr {
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(*bt.Elem)
		bt = *bt.Elem
	}
	l.emitFieldChain(bt, embedPath)
}

// concreteMethodCall lowers recv.Method(args) where recv's static type is a type
// parameter resolved (during monomorphization) to a concrete type. It looks up
// the method on the concrete type and emits a direct call.
func (l *funcLowerer) concreteMethodCall(e *ast.CallExpr, sel *ast.SelectorExpr, concrete types.Type, name string) goir.Type {
	obj, _, _ := types.LookupFieldOrMethod(concrete, true, l.pkg.Types, name)
	cfn, _ := obj.(*types.Func)
	if cfn == nil {
		l.fail(e.Pos(), "method "+name+" on "+concrete.String())
		return goir.TVoid
	}
	m := l.byFunc[cfn]
	if m == nil {
		l.fail(e.Pos(), "method "+name+" on "+concrete.String())
		return goir.TVoid
	}
	sig := cfn.Type().(*types.Signature)
	recvIsPtr := isPointerType(sig.Recv().Type())
	baseType := l.exprType(sel.X)
	baseIsPtr := baseType.Kind == goir.KPtr
	switch {
	case recvIsPtr && baseIsPtr:
		l.expr(sel.X)
	case recvIsPtr && !baseIsPtr:
		if !l.emitAddr(sel.X) {
			l.fail(e.Pos(), "pointer-receiver method on a non-addressable value")
			return goir.TVoid
		}
	case !recvIsPtr && baseIsPtr:
		l.expr(sel.X)
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(*baseType.Elem)
	default:
		l.expr(sel.X)
	}
	l.emitCallArgs(e.Args, m.Params[1:], sig.Variadic(), e.Ellipsis.IsValid())
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	return m.Ret
}

func isNilIdent(e ast.Expr) bool {
	id, ok := unparen(e).(*ast.Ident)
	return ok && id.Name == "nil"
}

// methodCall lowers recv.Method(args), adapting the receiver between value and
// pointer forms as Go does implicitly.
func (l *funcLowerer) methodCall(e *ast.CallExpr, sel *ast.SelectorExpr, seln *types.Selection) goir.Type {
	fn, ok := seln.Obj().(*types.Func)
	if !ok {
		l.fail(e.Pos(), "method call")
		return goir.TVoid
	}
	// Method on a shimmed stdlib type (reflect.Type.Kind, …) -> external call.
	if ext, ok := l.shimMethodExtern(seln); ok {
		return l.shimMethodCall(e, sel, ext)
	}

	// Method on a type-parameter value inside a monomorphized generic: the
	// receiver's concrete type is known, so call it directly rather than treating
	// the type parameter's constraint as an interface to dispatch on.
	recvT := l.pkg.TypesInfo.TypeOf(sel.X)
	if tp, ok := recvT.(*types.TypeParam); ok && l.typeSubst != nil {
		if concrete, ok := l.typeSubst[tp]; ok {
			return l.concreteMethodCall(e, sel, concrete, fn.Name())
		}
	}
	// Interface method call: dispatch on the dynamic type.
	if iface, ok := recvT.Underlying().(*types.Interface); ok {
		return l.interfaceDispatch(e, func() { l.expr(sel.X) }, fn, iface)
	}
	// Method promoted from an *embedded interface field* of a concrete receiver
	// (struct { SomeIface }): the method's receiver is an interface, so navigate to
	// the embedded interface value and dispatch on it.
	if iface, ok := fn.Type().(*types.Signature).Recv().Type().Underlying().(*types.Interface); ok {
		if idx := seln.Index(); len(idx) > 1 {
			return l.interfaceDispatch(e, func() { l.emitEmbeddedIfaceValue(sel, idx[:len(idx)-1]) }, fn, iface)
		}
	}
	m := l.byFunc[fn]
	if m == nil {
		// A method on a generic type is monomorphized per receiver instantiation.
		if gm, ok := l.instantiateMethod(fn, seln); ok {
			m = gm
		} else {
			l.fail(e.Pos(), "call to method "+fn.Name())
			return goir.TVoid
		}
	}
	sig := fn.Type().(*types.Signature)
	recvIsPtr := isPointerType(sig.Recv().Type())

	// Promoted method: go/types' selection index lists the embedded field path
	// (all but the last element, which is the method) to reach the receiver.
	if idx := seln.Index(); len(idx) > 1 {
		return l.promotedMethodCall(e, sel, fn, m, idx[:len(idx)-1], recvIsPtr, sig)
	}

	baseType := l.exprType(sel.X)
	baseIsPtr := baseType.Kind == goir.KPtr

	var writeback func() // for the pointer-rooted-field receiver RMW (see below)
	switch {
	case recvIsPtr && baseIsPtr:
		l.expr(sel.X) // pass the pointer through
	case recvIsPtr && !baseIsPtr:
		// (&recv).Method(): recv was marked address-taken, so it is a cell.
		if l.emitAddr(sel.X) {
			break
		}
		// A value-struct field reached through a pointer (p.A.B with B a value
		// struct) is not directly addressable; read it into a fresh GoPtr cell for
		// the call and write it back afterwards so the method's mutations persist.
		if p, wb, ok := l.ptrRootedReceiver(sel.X); ok {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: p})
			writeback = wb
			break
		}
		// A non-struct field reached through a pointer/global/cell (e.g. a named-slice
		// field p.items.Method()): a field-alias pointer aliases the live storage, so
		// the method's mutations persist with no separate writeback.
		if fsel, ok := unparen(sel.X).(*ast.SelectorExpr); ok && l.buildFieldAlias(fsel) {
			break
		}
		// A package-level global value g.Method(): alias the global directly.
		if gi, ok := l.globalRef(sel.X); ok {
			l.emitGlobalAlias(gi, l.exprType(sel.X))
			break
		}
		// A slice element s[i].Method(): &s[i] aliases the backing array element.
		if ix, ok := unparen(sel.X).(*ast.IndexExpr); ok && l.exprType(ix.X).Kind == goir.KSlice {
			xt := l.exprType(ix.X)
			l.expr(ix.X)
			l.expr(ix.Index)
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ElemAddr",
				Params: []goir.Type{xt, goir.TInt64}, Ret: goir.PtrType(*xt.Elem),
			}})
			break
		}
		l.fail(e.Pos(), "pointer-receiver method on a non-addressable value")
		return goir.TVoid
	case !recvIsPtr && baseIsPtr:
		// (*recv).Method(): dereference to a value.
		l.expr(sel.X)
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(*baseType.Elem)
	default:
		l.expr(sel.X) // value receiver, value base
	}

	// params[0] is the receiver; the rest align with the call arguments.
	l.emitCallArgs(e.Args, m.Params[1:], sig.Variadic(), e.Ellipsis.IsValid())
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	if writeback != nil {
		writeback() // persist mutations back into the pointer-rooted field (stack-neutral)
	}
	return m.Ret
}

// ptrRootedReceiver prepares the receiver for a pointer-receiver method call whose
// receiver is a value-struct field that is not directly addressable — a field
// reached through a pointer (p.A.B) or a field of a cell-held local struct (r.cc).
// Its current value is read into a fresh GoPtr cell for the call; the returned
// writeback thunk stores the (possibly mutated) value back into the field
// afterwards. ok is false when no supported writeback strategy applies.
func (l *funcLowerer) ptrRootedReceiver(recv ast.Expr) (int, func(), bool) {
	sel, ok := unparen(recv).(*ast.SelectorExpr)
	if !ok {
		return 0, nil, false
	}
	recvT := l.exprType(sel)
	// The RMW boxes the field value into a GoPtr for the call and writes it back, so
	// it works for any value-type field (a struct, or a named primitive like a
	// `type streamSafe uint8` with a pointer-receiver method) — but not reference
	// kinds whose receiver semantics differ.
	switch recvT.Kind {
	case goir.KStruct, goir.KInt64, goir.KInt32, goir.KUint64, goir.KUint32,
		goir.KFloat64, goir.KFloat32, goir.KBool, goir.KString:
	default:
		return 0, nil, false
	}

	// Pick a writeback strategy that mirrors the value the GoPtr cell carries.
	pushFromCell := func(p int) {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: p})
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(recvT)
	}
	var writeback func(p int)
	switch {
	case l.isPtrRootedField(sel):
		// p.A.B: write back through the pointer (read *p, set field, store).
		writeback = func(p int) {
			l.pointerRootedFieldWriteVal(sel, func(parent *goir.Struct, fi int) { pushFromCell(p) })
		}
	default:
		bt := l.exprType(sel.X)
		if bt.Kind != goir.KStruct {
			return 0, nil, false
		}
		fi := bt.Struct.FieldIndex(sel.Sel.Name)
		if fi < 0 {
			return 0, nil, false
		}
		if gi, ok := l.globalRef(sel.X); ok {
			// g.field where g is a package-level struct global: RMW through the global.
			writeback = func(p int) {
				l.globalFieldModify(gi, bt, fi, func(ft goir.Type) { pushFromCell(p) })
			}
		} else if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
			// r.cc where r is a cell-held local struct: RMW through the cell.
			cfi := elem.Struct.FieldIndex(sel.Sel.Name)
			if cfi < 0 {
				return 0, nil, false
			}
			writeback = func(p int) {
				l.cellFieldModify(idx, elem, cfi, func(ft goir.Type) { pushFromCell(p) })
			}
		} else {
			// r.cc where r is an addressable local struct (or field chain rooted at
			// one): write the field through its managed address.
			writeback = func(p int) {
				l.lvalueAddr(sel.X)
				pushFromCell(p)
				l.emit(goir.Op{Code: goir.OpStFld, Struct: bt.Struct, Field: fi})
			}
		}
	}

	l.expr(sel) // current field value
	l.emitBox(recvT)
	l.ptrNew(recvT)
	p := l.addLocal(nil, goir.PtrType(recvT))
	l.emit(goir.Op{Code: goir.OpStLoc, Local: p})
	return p, func() { writeback(p) }, true
}

// isPtrRootedField reports whether a selector chain has a *struct somewhere up its
// base (so the target field lives inside a pointed-to struct).
func (l *funcLowerer) isPtrRootedField(sel *ast.SelectorExpr) bool {
	for cur := ast.Expr(sel); ; {
		s, ok := unparen(cur).(*ast.SelectorExpr)
		if !ok {
			return false
		}
		bt := l.exprType(s.X)
		if bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KStruct {
			return true
		}
		if bt.Kind != goir.KStruct {
			return false
		}
		cur = s.X
	}
}

// promotedMethodCall lowers recv.M(args) where M is promoted from an embedded
// field: embedPath is the chain of field indices to the embedded value/pointer
// that actually owns M. The embedded receiver is loaded by walking the field
// chain, then adapted to the method's value/pointer receiver.
func (l *funcLowerer) promotedMethodCall(e *ast.CallExpr, sel *ast.SelectorExpr, fn *types.Func, m *goir.Method, embedPath []int, recvIsPtr bool, sig *types.Signature) goir.Type {
	baseType := l.exprType(sel.X)

	// A pointer-receiver method promoted through a *value* embed needs the address
	// of the embedded field, which a boxed value-type does not expose stably. Walk
	// the path on an addressable copy and write it back through the cell.
	if recvIsPtr {
		// Determine the embedded receiver type by walking the path on types only.
		embT := baseType
		if embT.Kind == goir.KPtr {
			embT = *embT.Elem
		}
		for _, idx := range embedPath {
			if embT.Kind == goir.KPtr {
				embT = *embT.Elem
			}
			embT = embT.Struct.Fields[idx].Type
		}
		if embT.Kind != goir.KPtr {
			// value embed + pointer receiver: needs &embedded with write-back.
			return l.promotedPtrRecvValueEmbed(e, sel, m, embedPath, sig)
		}
		// Pointer embed: the chain yields the pointer itself — pass it through.
	}

	// Load the base value (dereferencing a pointer base), then walk to the embed.
	if baseType.Kind == goir.KPtr {
		l.expr(sel.X)
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(*baseType.Elem)
		baseType = *baseType.Elem
	} else {
		l.expr(sel.X)
	}
	embType := l.emitFieldChain(baseType, embedPath)
	if !recvIsPtr && embType.Kind == goir.KPtr {
		// value receiver, pointer embed: dereference to the value.
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(*embType.Elem)
	}

	l.emitCallArgs(e.Args, m.Params[1:], sig.Variadic(), e.Ellipsis.IsValid())
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	return m.Ret
}

// promotedPtrRecvValueEmbed handles u.M() where M has a pointer receiver promoted
// through value-typed embedded field(s). goclr pointer receivers are GoPtr cells,
// not CLR byrefs, so it: copies the base into a local, lifts the embedded value
// into a fresh GoPtr cell, calls M on that cell (mutating it), writes the mutated
// value back into the base local (ldflda chain), then stores the base back through
// the receiver's own cell so the mutation is observed by the caller.
func (l *funcLowerer) promotedPtrRecvValueEmbed(e *ast.CallExpr, sel *ast.SelectorExpr, m *goir.Method, embedPath []int, sig *types.Signature) goir.Type {
	baseType := l.exprType(sel.X)
	baseIsPtr := baseType.Kind == goir.KPtr
	if baseIsPtr {
		baseType = *baseType.Elem
	}
	// Only all-value embed paths are addressable inside the local; a pointer embed
	// in the path would alias differently — keep that out of this path.
	cur := baseType
	for _, idx := range embedPath {
		if cur.Kind != goir.KStruct {
			l.fail(e.Pos(), "promoted pointer-receiver method through a mixed embed")
			return goir.TVoid
		}
		cur = cur.Struct.Fields[idx].Type
	}
	embType := cur

	tmp := l.addLocal(nil, baseType)
	l.expr(sel.X)
	if baseIsPtr {
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(baseType)
	}
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})

	// Lift the embedded value into a GoPtr cell.
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	l.emitFieldChain(baseType, embedPath)
	l.emitBox(embType)
	l.ptrNew(embType)
	cellLoc := l.addLocal(nil, goir.PtrType(embType))
	l.emit(goir.Op{Code: goir.OpStLoc, Local: cellLoc})

	// Call M with the cell as receiver.
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: cellLoc})
	l.emitCallArgs(e.Args, m.Params[1:], sig.Variadic(), e.Ellipsis.IsValid())
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	ret := m.Ret
	if ret != goir.TVoid {
		// Stash the result; the write-back must run before we leave it on the stack.
		rtmp := l.addLocal(nil, ret)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: rtmp})
		l.writeBackEmbed(tmp, baseType, cellLoc, embType, embedPath)
		l.storeBaseThroughCell(sel, tmp, baseType)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: rtmp})
		return ret
	}
	l.writeBackEmbed(tmp, baseType, cellLoc, embType, embedPath)
	l.storeBaseThroughCell(sel, tmp, baseType)
	return ret
}

// writeBackEmbed stores the (possibly mutated) embedded value held in cellLoc back
// into the embedded field of the base struct in local tmp, via a ldflda chain.
func (l *funcLowerer) writeBackEmbed(tmp int, baseType goir.Type, cellLoc int, embType goir.Type, embedPath []int) {
	l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
	cur := baseType
	for i, idx := range embedPath {
		if i == len(embedPath)-1 {
			// Push the mutated embedded value, then stfld into the parent struct.
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: cellLoc})
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emitUnbox(embType)
			l.emit(goir.Op{Code: goir.OpStFld, Struct: cur.Struct, Field: idx})
			return
		}
		l.emit(goir.Op{Code: goir.OpLdFldA, Struct: cur.Struct, Field: idx})
		cur = cur.Struct.Fields[idx].Type
	}
}

// storeBaseThroughCell writes the base struct in local tmp back through the
// receiver's GoPtr cell, when the receiver is an addressable struct local.
func (l *funcLowerer) storeBaseThroughCell(sel *ast.SelectorExpr, tmp int, baseType goir.Type) {
	if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emitBox(baseType)
		l.emit(goir.Op{Code: goir.OpPtrSet})
	}
}
