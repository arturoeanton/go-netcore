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
func (l *funcLowerer) interfaceDispatch(e *ast.CallExpr, emitRecv func(), ifaceMethod *types.Func, iface *types.Interface) goir.Type {
	sig := ifaceMethod.Type().(*types.Signature)

	// Evaluate arguments once into temps (coerced to the method's param types). A
	// variadic method gets one temp per fixed parameter plus a final temp holding the
	// packed variadic slice — so a no-arg call like m.Match() still passes a (nil)
	// slice, matching the lowered method's arity.
	var argTmps []int
	if sig.Variadic() {
		nFixed := sig.Params().Len() - 1
		for i := 0; i < nFixed; i++ {
			pt, _ := l.goType(sig.Params().At(i).Type())
			tmp := l.addLocal(nil, pt)
			l.exprCoerced(e.Args[i], pt)
			l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
			argTmps = append(argTmps, tmp)
		}
		sliceT, _ := l.goType(sig.Params().At(nFixed).Type())
		tmp := l.addLocal(nil, sliceT)
		if e.Ellipsis.IsValid() {
			l.exprCoerced(e.Args[nFixed], sliceT) // m.Match(slice...) — slice passed directly
		} else {
			l.packVariadic(e.Args[nFixed:], *sliceT.Elem)
		}
		l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		argTmps = append(argTmps, tmp)
	} else {
		argTmps = make([]int, len(e.Args))
		for i, a := range e.Args {
			pt, _ := l.goType(sig.Params().At(i).Type())
			tmp := l.addLocal(nil, pt)
			l.exprCoerced(a, pt)
			l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
			argTmps[i] = tmp
		}
	}
	return l.interfaceDispatchCore(emitRecv, ifaceMethod, iface, argTmps)
}

// interfaceDispatchCore generates the isinst/typed-box/pointer matching over the
// interface's implementers and calls the matching concrete method with the
// receiver (from emitRecv) and the already-evaluated argument temps. Shared by a
// direct interface call and a bound interface method value.
func (l *funcLowerer) interfaceDispatchCore(emitRecv func(), ifaceMethod *types.Func, iface *types.Interface, argTmps []int) goir.Type {
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

	iTmp := l.addLocal(nil, goir.TObject)
	emitRecv()
	l.emit(goir.Op{Code: goir.OpStLoc, Local: iTmp})

	impls := l.implementers(iface)
	end := l.label()
	labels := make([]int, len(impls))
	ctypes := make([]goir.Type, len(impls)) // boxed value type (value impl) or pointee type (ptr impl)
	callees := make([]*goir.Method, len(impls))
	// A value implementer whose named type is a non-struct (e.g. a named slice
	// satisfying sort.Interface) is carried in the interface as a GoNamed (the typed
	// box): match it by type id, not by representation isinst. namedId[i] != 0 marks
	// such implementers. Struct value implementers keep their distinct CLR type, and
	// pointer implementers keep their GoPtr type id — both unchanged.
	namedId := make([]int64, len(impls))
	// embedField[i] >= 0 marks an implementer that satisfies the interface only via
	// an *embedded interface field* (struct { SomeIface }); the method is promoted
	// from that field. Such a value is dispatched by unwrapping to the embedded
	// interface value and re-dispatching (a loop, so nested wrappers terminate at a
	// concrete implementer).
	embedField := make([]int, len(impls))
	// recvPath[i] is the embedded-field index chain to the receiver when an
	// implementer satisfies the method via a method promoted from an embedded
	// *concrete* field (e.g. type valueUndefined struct{ valueNull }: Export is
	// valueNull's, so the dispatch must pass the embedded valueNull, not the outer
	// value). Empty for a directly-declared method.
	recvPath := make([][]int, len(impls))
	for i, impl := range impls {
		labels[i] = l.label()
		embedField[i] = -1
		ctypes[i], _ = l.goType(impl.named)
		recv := types.Type(impl.named)
		if impl.viaPtr {
			recv = types.NewPointer(impl.named)
		} else if id, ok := l.namedIdentity(impl.named); ok {
			namedId[i] = id
		}
		obj, idxPath, _ := types.LookupFieldOrMethod(recv, true, l.pkg.Types, ifaceMethod.Name())
		fn, _ := obj.(*types.Func)
		if fn != nil {
			callees[i] = l.byFunc[fn]
			// A promoted method (idxPath longer than the method itself) reached through
			// embedded value fields: record the field chain so the case body navigates
			// to the embedded receiver before calling.
			if callees[i] != nil && len(idxPath) > 1 {
				recvPath[i] = idxPath[:len(idxPath)-1]
			}
		}
		if callees[i] == nil {
			if fn != nil && len(idxPath) >= 1 {
				if _, isIface := fn.Type().(*types.Signature).Recv().Type().Underlying().(*types.Interface); isIface {
					embedField[i] = idxPath[0]
				}
			}
			if embedField[i] < 0 {
				l.fail(ifaceMethod.Pos(), "interface method "+ifaceMethod.Name()+" on "+impl.named.Obj().Name())
				return goir.TVoid
			}
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

	// loopStart re-enters the type matching after an embedded-interface unwrap, so a
	// value whose dynamic type wraps the interface (struct { SomeIface }) resolves to
	// the concrete implementer it ultimately holds.
	loopStart := l.label()
	l.mark(loopStart)
	for i, impl := range impls {
		if !impl.viaPtr {
			if namedId[i] != 0 {
				// Typed-box value implementer: match by named-type id.
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.namedIdExtern()})
				l.emit(goir.Op{Code: goir.OpLdcI8, Int: namedId[i]})
				l.emit(goir.Op{Code: goir.OpCeq})
				l.emit(goir.Op{Code: goir.OpBrTrue, Label: labels[i]})
				continue
			}
			// Value implementer: the boxed concrete value's .NET type is the struct.
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: ctypes[i]})
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: labels[i]})
			continue
		}
		// Pointer-to-non-struct implementer (e.g. method on *MyInt): the cell carries
		// no struct id, so disambiguate only as a GoPtr. Ambiguous when several such
		// pointer types implement the same interface (rare); see LIMITATIONS.md.
		ptrType := goir.PtrType(ctypes[i])
		if ctypes[i].Kind != goir.KStruct {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: ptrType})
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: labels[i]})
			continue
		}
		// Pointer-to-struct implementer: it is a GoPtr; disambiguate by the cell's id.
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
		// Embedded-interface implementer: unwrap to the embedded interface value and
		// re-dispatch (the method is promoted from that field).
		if embedField[i] >= 0 {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			l.emit(goir.Op{Code: goir.OpLdFld, Struct: ctypes[i].Struct, Field: embedField[i]})
			l.emit(goir.Op{Code: goir.OpStLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpBr, Label: loopStart})
			continue
		}
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		if impl.viaPtr {
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])}) // the GoPtr receiver
			// A value-receiver method promoted from an embedded field, reached through a
			// pointer implementer: deref to the struct and navigate to the embedded value.
			if len(recvPath[i]) > 0 {
				l.emit(goir.Op{Code: goir.OpPtrGet})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
				l.emitEmbedNav(ctypes[i], recvPath[i], callees[i].Params[0])
			}
		} else {
			if namedId[i] != 0 {
				l.emitUnwrapNamed() // GoNamed -> inner boxed representation
			}
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			// Method promoted from an embedded value field: navigate to the embedded
			// receiver (the called method expects that field's type, not the outer one).
			l.emitEmbedNav(ctypes[i], recvPath[i], callees[i].Params[0])
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
		// Converting a named non-struct value into an interface tags it with its
		// named-type identity (the typed box), so dispatch/fmt/%T can recover it.
		l.maybeWrapNamed(l.pkg.TypesInfo.TypeOf(e))
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
	// x.(I) where I is a non-empty interface: the result is x re-typed as I if its
	// dynamic type implements I, else a panic.
	if iface, ok := l.assertIface(e.Type); ok {
		tmp := l.addLocal(nil, goir.TObject)
		l.emitInterfaceAssert(func() { l.expr(e.X) }, iface, tmp)
		ok2, bad := l.label(), l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: ok2})
		l.mark(bad)
		l.emit(goir.Op{Code: goir.OpStrConst, Str: "interface conversion: interface does not implement the requested interface"})
		l.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TString})
		l.emit(goir.Op{Code: goir.OpCallPanic})
		l.mark(ok2)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		return
	}
	l.expr(e.X)
	l.emitUnbox(t)
}

// assertIface reports the underlying *types.Interface of a type-assertion target
// when it is a non-empty interface (so the assert checks interface satisfaction,
// not a concrete representation).
func (l *funcLowerer) assertIface(typeExpr ast.Expr) (*types.Interface, bool) {
	iface, ok := l.pkg.TypesInfo.TypeOf(typeExpr).Underlying().(*types.Interface)
	if !ok || iface.NumMethods() == 0 {
		return nil, false
	}
	return iface, true
}

// emitInterfaceAssert stores into resTmp the value (from valEmit) re-typed as the
// interface if its dynamic type implements iface, else null — the matched/not form
// the comma-ok and single-value assertions both build on.
func (l *funcLowerer) emitInterfaceAssert(valEmit func(), iface *types.Interface, resTmp int) {
	valEmit()
	l.emit(goir.Op{Code: goir.OpStLoc, Local: resTmp})
	matched, done := l.label(), l.label()
	for _, impl := range l.implementers(iface) {
		ct, _ := l.goType(impl.named)
		switch {
		case impl.viaPtr && ct.Kind != goir.KStruct:
			// Pointer to a non-struct: identify only as a GoPtr (no cell struct id).
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: resTmp})
			l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: goir.PtrType(ct)})
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: matched})
		case impl.viaPtr:
			skip := l.label()
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: resTmp})
			l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: goir.PtrType(ct)})
			l.emit(goir.Op{Code: goir.OpBrFalse, Label: skip})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: resTmp})
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ct)})
			l.emit(goir.Op{Code: goir.OpPtrTypeId})
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(ct.Struct.Id)})
			l.emit(goir.Op{Code: goir.OpCeq})
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: matched})
			l.mark(skip)
		default:
			if id, ok := l.namedIdentity(impl.named); ok {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: resTmp})
				l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.namedIdExtern()})
				l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
				l.emit(goir.Op{Code: goir.OpCeq})
				l.emit(goir.Op{Code: goir.OpBrTrue, Label: matched})
			} else {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: resTmp})
				l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: ct})
				l.emit(goir.Op{Code: goir.OpBrTrue, Label: matched})
			}
		}
	}
	// A runtime/stdlib GoError satisfies an interface whose method set is Error().
	if ifaceIsError(iface) {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: resTmp})
		l.emit(goir.Op{Code: goir.OpIsInstGoError})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: matched})
	}
	l.emit(goir.Op{Code: goir.OpLdNull})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: resTmp})
	l.emit(goir.Op{Code: goir.OpBr, Label: done})
	l.mark(matched)
	l.mark(done)
}

// emitEmbedNav navigates a chain of embedded-field indices from a struct value on
// the stack to the embedded receiver of a promoted method, dereferencing any
// embedded pointer field along the way. The receiver is left ready for callee: if
// the promoted method has a pointer receiver (recvType is a pointer), the embedded
// value is boxed into a fresh cell so a GoPtr is passed (correct for the read-only
// promoted methods dispatched here; a mutating one would see a copy).
func (l *funcLowerer) emitEmbedNav(start goir.Type, path []int, recvType goir.Type) {
	cur := start
	for _, fi := range path {
		if cur.Kind == goir.KPtr {
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emitUnbox(*cur.Elem)
			cur = *cur.Elem
		}
		if cur.Kind != goir.KStruct || fi >= len(cur.Struct.Fields) {
			return // not navigable (e.g. an embedded interface, handled elsewhere)
		}
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: cur.Struct, Field: fi})
		cur = cur.Struct.Fields[fi].Type
	}
	// A pointer-receiver promoted method wants &embedded: box the value into a cell.
	if recvType.Kind == goir.KPtr && cur.Kind != goir.KPtr {
		l.emitBox(cur)
		l.ptrNew(cur)
	}
}

// ifaceIsError reports whether iface's method set is exactly the error interface's
// (an Error() string method), so a runtime GoError satisfies it.
func ifaceIsError(iface *types.Interface) bool {
	if iface.NumMethods() != 1 {
		return false
	}
	m := iface.Method(0)
	sig, ok := m.Type().(*types.Signature)
	if !ok || m.Name() != "Error" || sig.Params().Len() != 0 || sig.Results().Len() != 1 {
		return false
	}
	b, ok := sig.Results().At(0).Type().Underlying().(*types.Basic)
	return ok && b.Kind() == types.String
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

	// x.(I) where I is a non-empty interface: match by interface satisfaction
	// (isinst against the representation would match everything).
	if iface, ok := l.assertIface(ta.Type); ok {
		l.emitInterfaceAssert(func() { l.expr(ta.X) }, iface, isTmp)
		l.bindAssertResults(s, isTmp, t)
		return
	}

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

	l.bindAssertResults(s, isTmp, t)
}

// bindAssertResults binds the comma-ok results: ok = (isTmp != null), and the value
// = unbox(isTmp) to type t (the matched value), or t's zero value when isTmp is null.
func (l *funcLowerer) bindAssertResults(s *ast.AssignStmt, isTmp int, t goir.Type) {
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
func (l *funcLowerer) emitTypeMatch(valLocal int, gt types.Type, t goir.Type, matchLabel int) {
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
	// A named non-struct type (the typed box) carries identity in a GoNamed wrapper,
	// so its representation alone (KInt32, KSlice, …) can't be distinguished from a
	// plain value of the underlying type. Match by the wrapper's type id instead.
	if id, ok := l.namedIdentity(gt); ok {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.namedIdExtern()})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: matchLabel})
		return
	}
	// `case I:` where I is a non-empty interface: match by interface satisfaction,
	// not `isinst object` (which matches every boxed value). e.g. a type switch with
	// both `case String` (an interface) and `case *Object` must not route an *Object
	// into the String arm.
	if iface, ok := gt.Underlying().(*types.Interface); ok && iface.NumMethods() > 0 {
		tmp := l.addLocal(nil, goir.TObject)
		l.emitInterfaceAssert(func() { l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal}) }, iface, tmp)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: matchLabel})
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
			gt := l.pkg.TypesInfo.TypeOf(te)
			tt, ok := l.goType(gt)
			if !ok {
				l.fail(te.Pos(), "type switch case type")
				return
			}
			l.emitTypeMatch(xTmp, gt, tt, c.lbl)
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
				// declareLocal makes the binding a GoPtr cell when it is address-taken
				// (e.g. a pointer-receiver method called on it), so &v works.
				cc := c.cc
				vLocal, _ := l.declareLocal(obj, vt)
				l.initLocal(vLocal, func() {
					l.emit(goir.Op{Code: goir.OpLdLoc, Local: xTmp})
					if len(cc.List) == 1 && !isNilIdent(cc.List[0]) && vt.Kind != goir.KObject {
						// A typed-box value (named non-struct) is stored as a GoNamed
						// wrapper; strip it before unboxing to the representation.
						if _, ok := l.namedIdentity(l.pkg.TypesInfo.TypeOf(cc.List[0])); ok {
							l.emitUnwrapNamed()
						}
						l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: vt})
					}
				})
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
