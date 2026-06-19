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
		if id, ok := unparen(e).(*ast.Ident); ok {
			if v, ok := pkg.TypesInfo.Uses[id].(*types.Var); ok {
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
		case *ast.CallExpr:
			// A pointer-receiver method called on an addressable value takes
			// its address implicitly (u.PtrMethod() == (&u).PtrMethod()).
			if sel, ok := n.Fun.(*ast.SelectorExpr); ok {
				if seln := pkg.TypesInfo.Selections[sel]; seln != nil && seln.Kind() == types.MethodVal {
					if fn, ok := seln.Obj().(*types.Func); ok {
						if sig, ok := fn.Type().(*types.Signature); ok && isPointerType(sig.Recv().Type()) {
							if !isPointerType(pkg.TypesInfo.TypeOf(sel.X)) {
								mark(sel.X)
							}
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

// addrOf lowers &operand, leaving a GoPtr on the stack.
func (l *funcLowerer) addrOf(e *ast.UnaryExpr) {
	switch x := unparen(e.X).(type) {
	case *ast.Ident:
		if !l.emitAddr(x) {
			l.fail(e.Pos(), "address of "+x.Name)
		}
	case *ast.CompositeLit:
		// &T{...}: a fresh cell holding the composite value.
		t := l.compositeLit(x)
		l.emitBox(t)
		l.ptrNew(t)
	default:
		l.fail(e.Pos(), "address-of (only &variable and &T{...} are supported)")
	}
}

// derefRead lowers *p (read), leaving the pointee value.
func (l *funcLowerer) derefRead(e *ast.StarExpr) {
	pt := l.exprType(e.X)
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
	l.emitBoxedZero(t)
	l.ptrNew(t)
	return goir.PtrType(t)
}

// ptrStructFieldRead lowers p.f where p is *struct (auto-dereference).
func (l *funcLowerer) ptrStructFieldRead(e *ast.SelectorExpr, pt goir.Type) {
	st := *pt.Elem
	fi := st.Struct.FieldIndex(e.Sel.Name)
	if fi < 0 {
		l.fail(e.Pos(), "unknown field "+e.Sel.Name)
		return
	}
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(st)
	l.emit(goir.Op{Code: goir.OpLdFld, Struct: st.Struct, Field: fi})
}

// ptrStructFieldWrite lowers p.f = v where p is *struct: unbox the struct from
// the cell, mutate the field, and store it back.
func (l *funcLowerer) ptrStructFieldWrite(e *ast.SelectorExpr, pt goir.Type, rhs ast.Expr) {
	st := *pt.Elem
	fi := st.Struct.FieldIndex(e.Sel.Name)
	if fi < 0 {
		l.fail(e.Pos(), "unknown field "+e.Sel.Name)
		return
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
	l.expr(rhs)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})

	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sTmp})
	l.emitBox(st)
	l.emit(goir.Op{Code: goir.OpPtrSet})
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
		return l.interfaceDispatch(e, sel, fn, iface)
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
	baseType := l.exprType(sel.X)
	baseIsPtr := baseType.Kind == goir.KPtr

	switch {
	case recvIsPtr && baseIsPtr:
		l.expr(sel.X) // pass the pointer through
	case recvIsPtr && !baseIsPtr:
		// (&recv).Method(): recv was marked address-taken, so it is a cell.
		if !l.emitAddr(sel.X) {
			l.fail(e.Pos(), "pointer-receiver method on a non-addressable value")
			return goir.TVoid
		}
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
	return m.Ret
}
