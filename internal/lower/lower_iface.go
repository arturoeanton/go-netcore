package lower

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// ifaceImpl is a named type satisfying an interface. viaPtr is set when only the
// pointer method set satisfies it (a pointer-receiver implementer), in which case
// the interface holds a GoPtr cell tagged with the type id rather than a boxed
// value.
type ifaceImpl struct {
	named  *types.Named
	viaPtr bool
}

// implementers returns the named types across all lowered packages whose value
// method set (dispatched via isinst on the boxed value) or pointer method set
// (dispatched via a GoPtr cell + type-id check) satisfies the interface. Scanning
// every package — not just the one being lowered — is what lets an interface
// defined in one package (e.g. sort.Interface) dispatch to an implementer defined
// in another (the caller's type), which cross-package generic functions require.
func (c *lowerCtx) implementers(iface *types.Interface) []ifaceImpl {
	var out []ifaceImpl
	seen := map[*types.Named]bool{}
	scopes := []*types.Scope{c.pkg.Types.Scope()}
	for pkg := range c.prefixOf {
		if pkg != c.pkg.Types {
			scopes = append(scopes, pkg.Scope())
		}
	}
	for _, scope := range scopes {
		for _, name := range scope.Names() {
			tn, ok := scope.Lookup(name).(*types.TypeName)
			if !ok {
				continue
			}
			named, ok := tn.Type().(*types.Named)
			if !ok || seen[named] {
				continue
			}
			if _, isIface := named.Underlying().(*types.Interface); isIface {
				continue
			}
			switch {
			case types.Implements(named, iface):
				seen[named] = true
				out = append(out, ifaceImpl{named: named})
			case types.Implements(types.NewPointer(named), iface):
				seen[named] = true
				out = append(out, ifaceImpl{named: named, viaPtr: true})
			}
		}
	}
	return out
}

// interfaceDispatch lowers i.M(args) by generating an isinst-based switch over
// the concrete types that implement the interface, calling the matching method.
func (l *funcLowerer) interfaceDispatch(e *ast.CallExpr, sel *ast.SelectorExpr, ifaceMethod *types.Func, iface *types.Interface) goir.Type {
	sig := ifaceMethod.Type().(*types.Signature)
	retType := goir.TVoid
	if sig.Results().Len() == 1 {
		retType, _ = l.goType(sig.Results().At(0).Type())
	} else if sig.Results().Len() > 1 {
		// Multiple results: the lowered method returns a boxed object[] tuple, which
		// the caller spreads (it sees a *types.Tuple). Dispatch stores/returns it
		// like any single reference result.
		retType = goir.TObjectArray
	}

	// Evaluate arguments once into temps (coerced to the method's param types).
	argTmps := make([]int, len(e.Args))
	for i, a := range e.Args {
		pt, _ := l.goType(sig.Params().At(i).Type())
		tmp := l.addLocal(nil, pt)
		l.exprCoerced(a, pt)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		argTmps[i] = tmp
	}

	iTmp := l.addLocal(nil, goir.TObject)
	l.expr(sel.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: iTmp})

	impls := l.implementers(iface)
	end := l.label()
	labels := make([]int, len(impls))
	ctypes := make([]goir.Type, len(impls)) // boxed value type (value impl) or pointee type (ptr impl)
	callees := make([]*goir.Method, len(impls))
	for i, impl := range impls {
		labels[i] = l.label()
		ctypes[i], _ = l.goType(impl.named)
		recv := types.Type(impl.named)
		if impl.viaPtr {
			recv = types.NewPointer(impl.named)
		}
		obj, _, _ := types.LookupFieldOrMethod(recv, true, l.pkg.Types, ifaceMethod.Name())
		fn, _ := obj.(*types.Func)
		if fn != nil {
			callees[i] = l.byFunc[fn]
		}
		if callees[i] == nil {
			l.fail(e.Pos(), "interface method "+ifaceMethod.Name()+" on "+impl.named.Obj().Name())
			return goir.TVoid
		}
	}

	// Runtime errors (errors.New, fmt.Errorf, stdlib returns) are GoError values,
	// not Go-package types, so add a fallback branch for the error interface's
	// Error() method.
	isErrorMethod := ifaceMethod.Name() == "Error" && sig.Params().Len() == 0 && retType == goir.TString
	goErrLabel := -1
	if isErrorMethod {
		goErrLabel = l.label()
	}

	// The result is computed into a temp so that every dispatch branch occurs at
	// the same evaluation-stack depth (important when the dispatch is itself a
	// sub-expression, e.g. a call argument).
	resultTmp := -1
	if retType != goir.TVoid {
		resultTmp = l.addLocal(nil, retType)
	}

	for i, impl := range impls {
		if !impl.viaPtr {
			// Value implementer: the boxed concrete value's .NET type is the struct.
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: ctypes[i]})
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: labels[i]})
			continue
		}
		// Pointer implementer: it is a GoPtr; disambiguate by the cell's type id.
		ptrType := goir.PtrType(ctypes[i])
		skip := l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: ptrType}) // isinst GoPtr
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: skip})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ptrType}) // castclass GoPtr
		l.emit(goir.Op{Code: goir.OpPtrTypeId})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(ctypes[i].Struct.Id)})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: labels[i]})
		l.mark(skip)
	}
	if goErrLabel >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpIsInstGoError})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: goErrLabel})
	}
	// No match => nil interface method call.
	l.emit(goir.Op{Code: goir.OpStrConst, Str: "runtime error: invalid memory address or nil pointer dereference"})
	l.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TString})
	l.emit(goir.Op{Code: goir.OpCallPanic})
	l.emit(goir.Op{Code: goir.OpBr, Label: end})

	for i, impl := range impls {
		l.mark(labels[i])
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		if impl.viaPtr {
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])}) // the GoPtr receiver
		} else {
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
		}
		for _, at := range argTmps {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: at})
		}
		l.emit(goir.Op{Code: goir.OpCallMethod, Callee: callees[i]})
		if resultTmp >= 0 {
			l.emit(goir.Op{Code: goir.OpStLoc, Local: resultTmp})
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
	}
	if goErrLabel >= 0 {
		l.mark(goErrLabel)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpIsInstGoError}) // value typed as GoError
		l.emit(goir.Op{Code: goir.OpErrorError})    // -> GoString
		l.emit(goir.Op{Code: goir.OpStLoc, Local: resultTmp})
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
	}
	l.mark(end)
	if resultTmp >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: resultTmp})
	}
	return retType
}

// exprCoerced lowers e and converts the result to the target type. The only
// conversion M2 performs is the implicit boxing of a concrete value into an
// interface ({}/any -> object).
func (l *funcLowerer) exprCoerced(e ast.Expr, target goir.Type) {
	if isNilIdent(e) {
		// nil into a slice slot is the value-type nil slice, not a null reference.
		if target.Kind == goir.KSlice {
			l.emitZeroValue(target)
			return
		}
		l.emit(goir.Op{Code: goir.OpLdNull})
		return
	}
	l.expr(e)
	if target.Kind == goir.KObject {
		l.emitBox(l.exprType(e))
		return
	}
	// Copying an array value (assignment, argument, return, field/element store)
	// duplicates its backing storage — arrays have value semantics, unlike slices.
	if target.Array {
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ArrayClone",
			Params: []goir.Type{target}, Ret: target,
		}})
	}
}

// typeAssert lowers the single-value form x.(T): unbox.any panics on mismatch.
func (l *funcLowerer) typeAssert(e *ast.TypeAssertExpr) {
	t, ok := l.goType(l.pkg.TypesInfo.TypeOf(e.Type))
	if !ok {
		l.fail(e.Pos(), "type assertion target")
		return
	}
	l.expr(e.X)
	l.emitUnbox(t)
}

// typeAssertOK lowers the comma-ok form v, ok := x.(T) using isinst.
func (l *funcLowerer) typeAssertOK(s *ast.AssignStmt) {
	ta := s.Rhs[0].(*ast.TypeAssertExpr)
	t, ok := l.goType(l.pkg.TypesInfo.TypeOf(ta.Type))
	if !ok {
		l.fail(ta.Pos(), "type assertion target")
		return
	}

	isTmp := l.addLocal(nil, goir.TObject)
	l.expr(ta.X)
	l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: t})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: isTmp})

	// For a pointer-to-struct assertion, isinst only proves the value is *some*
	// GoPtr; verify the cell's type id and null isTmp on mismatch.
	if t.Kind == goir.KPtr && t.Elem.Kind == goir.KStruct {
		lok := l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: lok})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
		l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: t}) // castclass GoPtr
		l.emit(goir.Op{Code: goir.OpPtrTypeId})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(t.Elem.Struct.Id)})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: lok})
		l.emit(goir.Op{Code: goir.OpLdNull})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: isTmp})
		l.mark(lok)
	}

	vIdx := l.assignTarget(s, s.Lhs[0], t)
	okIdx := l.assignTarget(s, s.Lhs[1], goir.TBool)

	if okIdx >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
		l.emit(goir.Op{Code: goir.OpLdNull})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpNot})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: okIdx})
	}
	if vIdx >= 0 {
		lz, lend := l.label(), l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: lz})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
		l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: t})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: vIdx})
		l.emit(goir.Op{Code: goir.OpBr, Label: lend})
		l.mark(lz)
		l.emitZeroValue(t)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: vIdx})
		l.mark(lend)
	}
}

// emitTypeMatch branches to matchLabel when the value in valLocal dynamically has
// type t. Pointer-to-struct cases also verify the GoPtr's type id, since all
// pointers share the GoPtr .NET type.
func (l *funcLowerer) emitTypeMatch(valLocal int, t goir.Type, matchLabel int) {
	if t.Kind == goir.KPtr && t.Elem.Kind == goir.KStruct {
		skip := l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
		l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: t}) // isinst GoPtr
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: skip})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
		l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: t}) // castclass GoPtr
		l.emit(goir.Op{Code: goir.OpPtrTypeId})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(t.Elem.Struct.Id)})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: matchLabel})
		l.mark(skip)
		return
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
	l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: t})
	l.emit(goir.Op{Code: goir.OpBrTrue, Label: matchLabel})
}

// typeSwitch lowers `switch v := x.(type) { case T: ... }`.
func (l *funcLowerer) typeSwitch(s *ast.TypeSwitchStmt) {
	if s.Init != nil {
		l.stmt(s.Init)
	}

	// Extract the asserted expression and the optional binding (v := x.(type)).
	var xExpr ast.Expr
	hasBinding := false
	switch a := s.Assign.(type) {
	case *ast.ExprStmt:
		xExpr = a.X.(*ast.TypeAssertExpr).X
	case *ast.AssignStmt:
		xExpr = a.Rhs[0].(*ast.TypeAssertExpr).X
		hasBinding = true
	default:
		l.fail(s.Pos(), "type switch")
		return
	}

	xTmp := l.addLocal(nil, goir.TObject)
	l.expr(xExpr)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: xTmp})

	end := l.label()
	type clause struct {
		cc  *ast.CaseClause
		lbl int
	}
	var clauses []clause
	defaultLbl := -1
	for _, st := range s.Body.List {
		cc := st.(*ast.CaseClause)
		lbl := l.label()
		clauses = append(clauses, clause{cc, lbl})
		if cc.List == nil {
			defaultLbl = lbl
		}
	}

	// Dispatch on the dynamic type.
	for _, c := range clauses {
		for _, te := range c.cc.List {
			if isNilIdent(te) {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: xTmp})
				l.emit(goir.Op{Code: goir.OpLdNull})
				l.emit(goir.Op{Code: goir.OpCeq})
				l.emit(goir.Op{Code: goir.OpBrTrue, Label: c.lbl})
				continue
			}
			tt, ok := l.goType(l.pkg.TypesInfo.TypeOf(te))
			if !ok {
				l.fail(te.Pos(), "type switch case type")
				return
			}
			l.emitTypeMatch(xTmp, tt, c.lbl)
		}
	}
	if defaultLbl >= 0 {
		l.emit(goir.Op{Code: goir.OpBr, Label: defaultLbl})
	} else {
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
	}

	// Bodies.
	l.breaks = append(l.breaks, end)
	for _, c := range clauses {
		l.mark(c.lbl)
		if hasBinding {
			if obj := l.pkg.TypesInfo.Implicits[c.cc]; obj != nil {
				vt, _ := l.goType(obj.Type())
				vLocal := l.addLocal(obj, vt)
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: xTmp})
				if len(c.cc.List) == 1 && !isNilIdent(c.cc.List[0]) && vt.Kind != goir.KObject {
					l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: vt})
				}
				l.emit(goir.Op{Code: goir.OpStLoc, Local: vLocal})
			}
		}
		for _, st := range c.cc.Body {
			l.stmt(st)
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
	}
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.mark(end)
}
