package lower

import (
	"go/ast"
	"go/types"
	"sort"

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
	// unsupported[i] marks an implementer reachable only because it incidentally
	// satisfies the interface (the closure of a large program has many such types),
	// whose method goclr can neither lower (it belongs to a C# shim type) nor map to
	// a shim extern — e.g. *bufConn in x/net/http2/h2c promoting ReadByte from an
	// embedded *bufio.Reader, enumerated as an io.ByteReader implementer though it
	// never flows to one. The type is still matched, but its case body panics: a
	// guarded, diagnosable failure only if such a value ever reaches this call site,
	// rather than aborting the whole compilation. Tracked in docs/LIMITATIONS.md.
	unsupported := make([]bool, len(impls))
	// shimExt[i] != nil marks an implementer that satisfies the method through an
	// embedded shim-type field (e.g. driverConn{ sync.Mutex } satisfying sync.Locker):
	// the case body navigates recvPath[i] to that field and calls the shim extern.
	shimExt := make([]*goir.Extern, len(impls))
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
			// The method belongs to a shim type (promoted from an embedded shim field):
			// call its extern, navigating to that field.
			if embedField[i] < 0 && fn != nil {
				if ext, ok := l.shimExternForFunc(fn); ok {
					shimExt[i] = ext
					if len(idxPath) > 1 {
						recvPath[i] = idxPath[:len(idxPath)-1]
					}
				}
			}
			if embedField[i] < 0 && shimExt[i] == nil {
				// The method resolves to a shim type's method with no lowered body and
				// no shim extern. Keep this implementer matchable but give it a panic
				// body instead of failing the whole compilation (see unsupported above).
				unsupported[i] = true
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
			// A *T pointer also satisfies T's value-receiver methods, so the interface
			// may hold a GoPtr to the struct; match it too (the body derefs it).
			if ctypes[i].Kind == goir.KStruct {
				skip := l.label()
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: goir.PtrType(ctypes[i])})
				l.emit(goir.Op{Code: goir.OpBrFalse, Label: skip})
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])})
				l.emit(goir.Op{Code: goir.OpPtrTypeId})
				l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(ctypes[i].Struct.Id)})
				l.emit(goir.Op{Code: goir.OpCeq})
				l.emit(goir.Op{Code: goir.OpBrTrue, Label: labels[i]})
				l.mark(skip)
			}
			continue
		}
		// Pointer-to-non-struct implementer (e.g. method on *MyInt): the cell carries
		// no struct id, so disambiguate only as a GoPtr. Ambiguous when several such
		// pointer types implement the same interface (rare); see docs/LIMITATIONS.md.
		ptrType := goir.PtrType(ctypes[i])
		if ctypes[i].Kind != goir.KStruct {
			// Refine the plain GoPtr match by the pointee's representation so distinct
			// pointer-to-non-struct implementers (*MyInt vs *MyString) are told apart.
			l.emitPtrPointeeMatch(iTmp, ptrType, labels[i])
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
	// An opaque shim value may flow through an interface it satisfies (the RWMutex that
	// database/sql's Rows locks through RLocker() as a sync.Locker; a syscall.Signal as
	// an os.Signal). Such handles share the System.Object CLR type, so no isinst branch
	// above matched; route each shim type whose method registry covers the interface to
	// its method, discriminating by the value's concrete CLR class (Rt.IsShimKind). This
	// is data-driven by the shim registries — no Go type is named here.
	for _, si := range l.shimIfaceImplementers(iface, ifaceMethod) {
		next := l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: si.goName})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "IsShimKindStrict",
			Params: []goir.Type{goir.TObject, goir.TString}, Ret: goir.TBool,
		}})
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: next})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp}) // the shim handle is the receiver
		for _, at := range argTmps {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: at})
		}
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: si.ext})
		if resultTmp >= 0 {
			l.emit(goir.Op{Code: goir.OpStLoc, Local: resultTmp})
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
		l.mark(next)
	}
	// No match => nil interface method call.
	l.emit(goir.Op{Code: goir.OpStrConst, Str: "runtime error: invalid memory address or nil pointer dereference"})
	l.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TString})
	l.emit(goir.Op{Code: goir.OpCallPanic})
	l.emit(goir.Op{Code: goir.OpBr, Label: end})

	for i, impl := range impls {
		l.mark(labels[i])
		// Implementer satisfying the method through an embedded shim field (sync.Mutex,
		// etc.): load the struct, navigate to the embedded shim handle, call the extern.
		if shimExt[i] != nil {
			if impl.viaPtr {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])})
				l.emit(goir.Op{Code: goir.OpPtrGet})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			} else if ctypes[i].Kind == goir.KStruct {
				notPtr, done := l.label(), l.label()
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: goir.PtrType(ctypes[i])})
				l.emit(goir.Op{Code: goir.OpBrFalse, Label: notPtr})
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])})
				l.emit(goir.Op{Code: goir.OpPtrGet})
				l.emit(goir.Op{Code: goir.OpBr, Label: done})
				l.mark(notPtr)
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.mark(done)
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			} else {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			}
			l.emitEmbedNav(ctypes[i], recvPath[i], shimExt[i].Params[0])
			for _, at := range argTmps {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: at})
			}
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: shimExt[i]})
			if resultTmp >= 0 {
				l.emit(goir.Op{Code: goir.OpStLoc, Local: resultTmp})
			}
			l.emit(goir.Op{Code: goir.OpBr, Label: end})
			continue
		}
		// Incidental implementer whose method goclr cannot dispatch (shim type method,
		// no extern): panic if such a value ever reaches this site. It is matched so the
		// failure is precise, not silently routed to another branch.
		if unsupported[i] {
			l.emit(goir.Op{Code: goir.OpStrConst, Str: "goclr: interface method " + ifaceMethod.Name() +
				" on " + impl.named.Obj().Name() + " is not supported (shim type method)"})
			l.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TString})
			l.emit(goir.Op{Code: goir.OpCallPanic})
			l.emit(goir.Op{Code: goir.OpBr, Label: end})
			continue
		}
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
		if impl.viaPtr {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])}) // the GoPtr receiver
			// A value-receiver method promoted from an embedded field, reached through a
			// pointer implementer: deref to the struct and navigate to the embedded value.
			if len(recvPath[i]) > 0 {
				l.emit(goir.Op{Code: goir.OpPtrGet})
				l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
				l.emitEmbedNav(ctypes[i], recvPath[i], callees[i].Params[0])
			}
		} else if namedId[i] != 0 {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emitUnwrapNamed() // GoNamed -> inner boxed representation
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			l.emitEmbedNav(ctypes[i], recvPath[i], callees[i].Params[0])
		} else if ctypes[i].Kind == goir.KStruct {
			// A value-receiver implementer reached with either a boxed value or a *T
			// pointer (matched above): deref the pointer form to the boxed struct.
			notPtr, done := l.label(), l.label()
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: goir.PtrType(ctypes[i])})
			l.emit(goir.Op{Code: goir.OpBrFalse, Label: notPtr})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(ctypes[i])})
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emit(goir.Op{Code: goir.OpBr, Label: done})
			l.mark(notPtr)
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.mark(done)
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
			l.emitEmbedNav(ctypes[i], recvPath[i], callees[i].Params[0])
		} else {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
			l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: ctypes[i]})
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
		st := l.exprType(e)
		l.emitBox(st)
		// A concrete pointer converted to an interface stays non-nil even when the
		// pointer is nil — Go keeps the dynamic type (`var p *T; var i any = p; i == nil`
		// is false). Box a possibly-nil GoPtr into a non-null GoPtr carrying the pointee's
		// id; a non-nil pointer passes through unchanged.
		if st.Kind == goir.KPtr {
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: l.pointerBoxID(st, l.pkg.TypesInfo.TypeOf(e))})
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "BoxNilPtr",
				Params: []goir.Type{st, goir.TInt64}, Ret: goir.TObject,
			}})
		}
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

// pointerBoxID returns the pointee type id a boxed pointer should carry: a struct's
// dispatch id, a named non-struct's typed-box id, else 0 (e.g. *int — no methods, the id
// only needs to make the interface non-nil). Used by exprCoerced so a nil concrete pointer
// boxed into an interface still resolves its dynamic type.
func (l *funcLowerer) pointerBoxID(st goir.Type, gt types.Type) int64 {
	if st.Elem != nil && st.Elem.Kind == goir.KStruct {
		return int64(st.Elem.Struct.Id)
	}
	if pt, ok := gt.Underlying().(*types.Pointer); ok {
		if id, ok := l.namedIdentity(pt.Elem()); ok {
			return id
		}
	}
	return 0
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
	// x.(T) where T is a typed box (named non-struct, or a composite whose element
	// types are tagged): match the GoNamed wrapper's id, then unwrap before unboxing.
	if id, ok := l.typeTagFor(l.pkg.TypesInfo.TypeOf(e.Type)); ok {
		tmp := l.addLocal(nil, goir.TObject)
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		good := l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.namedIdExtern()})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: good})
		// Mismatch: panic like Go ("interface conversion: <iface> is <actual>, not <T>"),
		// the same message as the concrete path, not a generic "does not implement".
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: typeDescStr(l.pkg.TypesInfo.TypeOf(e.X))})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: typeDescStr(l.pkg.TypesInfo.TypeOf(e.Type))})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "AssertFail",
			Params: []goir.Type{goir.TObject, goir.TString, goir.TString}, Ret: goir.TVoid,
		}})
		l.mark(good)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emitUnwrapNamed()
		l.emitUnbox(t)
		return
	}
	// A concrete-representation assertion (int, string, struct, *T): check the dynamic
	// type and panic like Go ("interface conversion: ... is X, not Y") on mismatch,
	// rather than letting the CLR unbox throw a raw InvalidCastException.
	valTmp := l.addLocal(nil, goir.TObject)
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: valTmp})
	good := l.label()
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: valTmp})
	l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: t})
	l.emit(goir.Op{Code: goir.OpBrTrue, Label: good})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: valTmp})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: typeDescStr(l.pkg.TypesInfo.TypeOf(e.X))})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: typeDescStr(l.pkg.TypesInfo.TypeOf(e.Type))})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "AssertFail",
		Params: []goir.Type{goir.TObject, goir.TString, goir.TString}, Ret: goir.TVoid,
	}})
	l.mark(good)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: valTmp})
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
			// Pointer to a non-struct (no cell struct id): refine the plain GoPtr match
			// by the pointee's representation so e.g. a *RawBytes implementer of Scanner
			// is not matched by a *int64 destination (both are otherwise just GoPtr).
			l.emitPtrPointeeMatch(resTmp, goir.PtrType(ct), matched)
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

// shimImpl is an opaque shim type that satisfies an interface, paired with the extern
// for the method being dispatched.
type shimImpl struct {
	goName string
	ext    *goir.Extern
}

// shimIfaceImplementers returns the opaque shim types that satisfy iface (checked with
// types.Implements — a precise, full-signature test, not a method-name guess), each
// paired with the extern for ifaceMethod. This lets a shim value flowing through an
// interface (a sync.RWMutex as sync.Locker, a syscall.Signal as os.Signal) dispatch to
// its method without goclr hardcoding any specific type — the set is derived from
// opaqueShimTypes + shimMethodRegistry + go/types. Sorted by name for determinism.
func (l *funcLowerer) shimIfaceImplementers(iface *types.Interface, ifaceMethod *types.Func) []shimImpl {
	if iface.NumMethods() == 0 {
		return nil
	}
	var out []shimImpl
	for typeName := range opaqueShimTypes {
		methods, ok := shimMethodRegistry[typeName]
		if !ok {
			continue
		}
		sf, ok := methods[ifaceMethod.Name()]
		if !ok {
			continue
		}
		named, ok := l.shimNamedType(typeName)
		if !ok {
			continue // the shim type is not in this program's import closure
		}
		if !types.Implements(named, iface) && !types.Implements(types.NewPointer(named), iface) {
			continue
		}
		out = append(out, shimImpl{goName: typeName, ext: l.shimIfaceExtern(sf, ifaceMethod)})
	}
	sort.Slice(out, func(i, j int) bool { return out[i].goName < out[j].goName })
	return out
}

// shimNamedType resolves an opaque-shim type name ("sync.RWMutex") to its *types.Named,
// scanning the program's import closure once. The scan starts from c.root (the main
// package, whose closure spans the whole program), NOT l.pkg — l.pkg is whichever package
// is being lowered when this is first called, and a narrow-closure package (e.g. io,
// lowered from source, imports only errors+sync) would otherwise freeze an incomplete map
// that misses shim types like bytes.Buffer reachable only from main.
func (l *funcLowerer) shimNamedType(name string) (*types.Named, bool) {
	if l.shimNamed == nil {
		l.shimNamed = map[string]*types.Named{}
		seen := map[*types.Package]bool{}
		var visit func(p *types.Package)
		visit = func(p *types.Package) {
			if p == nil || seen[p] {
				return
			}
			seen[p] = true
			scope := p.Scope()
			for _, n := range scope.Names() {
				if opaqueShimTypes[p.Path()+"."+n] {
					if tn, ok := scope.Lookup(n).(*types.TypeName); ok {
						if nm, ok := tn.Type().(*types.Named); ok {
							l.shimNamed[p.Path()+"."+n] = nm
						}
					}
				}
			}
			for _, imp := range p.Imports() {
				visit(imp)
			}
		}
		visit(l.root)
	}
	n, ok := l.shimNamed[name]
	return n, ok
}

// shimIfaceExtern builds the extern for a shim method dispatched through an interface,
// deriving the receiver (object) plus the parameter and result types from the interface
// method's signature.
func (l *funcLowerer) shimIfaceExtern(sf shimFunc, ifaceMethod *types.Func) *goir.Extern {
	sig := ifaceMethod.Type().(*types.Signature)
	params := []goir.Type{goir.TObject}
	for i := 0; i < sig.Params().Len(); i++ {
		pt, _ := l.goType(sig.Params().At(i).Type())
		params = append(params, pt)
	}
	ret := goir.TVoid
	switch sig.Results().Len() {
	case 0:
	case 1:
		ret, _ = l.goType(sig.Results().At(0).Type())
	default:
		ret = goir.TObjectArray
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Params: params, Ret: ret}
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

	// v, ok := x.(T) where T is a typed box (named non-struct or tagged composite):
	// ok is whether the GoNamed wrapper's id matches; the bound value is unwrapped.
	if id, ok := l.typeTagFor(l.pkg.TypesInfo.TypeOf(ta.Type)); ok {
		valTmp := l.addLocal(nil, goir.TObject)
		l.expr(ta.X)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: valTmp})
		matched, done := l.label(), l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valTmp})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.namedIdExtern()})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: matched})
		l.emit(goir.Op{Code: goir.OpLdNull})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: isTmp})
		l.emit(goir.Op{Code: goir.OpBr, Label: done})
		l.mark(matched)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valTmp})
		l.emitUnwrapNamed() // GoNamed -> the boxed representation bindAssertResults unboxes
		l.emit(goir.Op{Code: goir.OpStLoc, Local: isTmp})
		l.mark(done)
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
	} else if t.Kind == goir.KPtr {
		// Pointer to a non-struct: isinst only proves it is some GoPtr. Null isTmp unless
		// the pointee's representation matches t's (so `dest.(*int64)` rejects a *string).
		if code, ok := pointeeKindCode(*t.Elem); ok {
			lok := l.label()
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
			l.emit(goir.Op{Code: goir.OpBrFalse, Label: lok}) // already null
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: pointeeKindExtern()})
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: code})
			l.emit(goir.Op{Code: goir.OpCeq})
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: lok})
			l.emit(goir.Op{Code: goir.OpLdNull})
			l.emit(goir.Op{Code: goir.OpStLoc, Local: isTmp})
			l.mark(lok)
		}
	}

	l.bindAssertResults(s, isTmp, t)
}

// bindAssertResults binds the comma-ok results: ok = (isTmp != null), and the value
// = unbox(isTmp) to type t (the matched value), or t's zero value when isTmp is null.
func (l *funcLowerer) bindAssertResults(s *ast.AssignStmt, isTmp int, t goir.Type) {
	// Compute the bound value (matched -> unbox, else zero) into a temp, then store it
	// through assignToTarget — which handles ident (cell-aware), struct-field and
	// slice/map-element targets, e.g. `r.typ, _ = vals[0].(string)`.
	valTmp := l.addLocal(nil, t)
	lz, lend := l.label(), l.label()
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: lz})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
	l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: t})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: valTmp})
	l.emit(goir.Op{Code: goir.OpBr, Label: lend})
	l.mark(lz)
	l.emitZeroValue(t)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: valTmp})
	l.mark(lend)
	l.assignToTarget(s, s.Lhs[0], t, func() { l.emit(goir.Op{Code: goir.OpLdLoc, Local: valTmp}) })

	okTmp := l.addLocal(nil, goir.TBool)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: isTmp})
	l.emit(goir.Op{Code: goir.OpLdNull})
	l.emit(goir.Op{Code: goir.OpCeq})
	l.emit(goir.Op{Code: goir.OpNot})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: okTmp})
	l.assignToTarget(s, s.Lhs[1], goir.TBool, func() { l.emit(goir.Op{Code: goir.OpLdLoc, Local: okTmp}) })
}

// pointeeKindCode returns the Rt.PtrPointeeKind code for a non-struct pointee type
// whose representation is distinguishable at runtime (so *int64, *string and *[]byte
// can be told apart). ok is false for kinds that share a representation or have none,
// where the caller keeps the plain GoPtr match.
func pointeeKindCode(elem goir.Type) (int64, bool) {
	switch elem.Kind {
	case goir.KString:
		return 1, true
	case goir.KSlice:
		return 2, true
	case goir.KInt64:
		return 3, true
	case goir.KInt32:
		return 4, true
	case goir.KBool:
		return 5, true
	case goir.KFloat64:
		return 6, true
	case goir.KUint64:
		return 7, true
	case goir.KUint32:
		return 8, true
	case goir.KFloat32:
		return 9, true
	}
	return 0, false
}

// pointeeKindExtern is the Rt.PtrPointeeKind descriptor.
func pointeeKindExtern() *goir.Extern {
	return &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "PtrPointeeKind",
		Params: []goir.Type{goir.TObject}, Ret: goir.TInt64,
	}
}

// emitPtrPointeeMatch branches to matchLabel when valLocal is a GoPtr whose pointee
// representation matches t (a pointer to a distinguishable non-struct). Falls back to
// a plain GoPtr isinst when the pointee kind is not distinguishable.
func (l *funcLowerer) emitPtrPointeeMatch(valLocal int, t goir.Type, matchLabel int) {
	code, ok := pointeeKindCode(*t.Elem)
	if !ok {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
		l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: t})
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: matchLabel})
		return
	}
	skip := l.label()
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
	l.emit(goir.Op{Code: goir.OpIsInst, BoxTy: t}) // isinst GoPtr
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: skip})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: pointeeKindExtern()})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: code})
	l.emit(goir.Op{Code: goir.OpCeq})
	l.emit(goir.Op{Code: goir.OpBrTrue, Label: matchLabel})
	l.mark(skip)
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
	// A typed box (named non-struct, or a tagged composite like []int) carries identity
	// in a GoNamed wrapper, so its representation alone (KInt32, KSlice, …) can't be
	// distinguished from a plain value of the underlying type. Match by the id instead.
	if id, ok := l.typeTagFor(gt); ok {
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
	// A pointer to a non-struct (e.g. *int64, *string): isinst alone only proves it is
	// some GoPtr, so refine by the pointee's runtime representation when distinguishable.
	if t.Kind == goir.KPtr && t.Elem.Kind != goir.KStruct {
		l.emitPtrPointeeMatch(valLocal, t, matchLabel)
		return
	}
	// An opaque shim type (time.Time, sync.Mutex, …) lowers to System.Object, so
	// `isinst object` would match *every* boxed value — a type switch `case time.Time`
	// must not capture a plain int64. Discriminate by the concrete shim CLR class via the
	// PRECISE matcher: only a value whose registered [GoShim] class is exactly this type
	// matches. The loose IsShimKind heuristic ("any unregistered non-primitive") falsely
	// matched a plain GoError against a never-instantiated opaque error stub
	// (`case *json.SyntaxError` capturing errors.New) — strict matching is correct here.
	if t.Kind == goir.KObject && t.Shim != "" {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: valLocal})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: t.Shim})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "IsShimKindStrict",
			Params: []goir.Type{goir.TObject, goir.TString}, Ret: goir.TBool,
		}})
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
						// A typed-box value (named non-struct or tagged composite) is stored
						// as a GoNamed wrapper; strip it before unboxing to the representation.
						if _, ok := l.typeTagFor(l.pkg.TypesInfo.TypeOf(cc.List[0])); ok {
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
