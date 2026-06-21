package lower

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// hasDefer reports whether a function body contains a defer statement at any
// nesting (but not inside a nested function literal, whose defers belong to it).
func hasDefer(fd *ast.FuncDecl) bool {
	return fd.Body != nil && blockHasDefer(fd.Body)
}

// blockHasDefer reports whether the (block) node contains a defer, not descending
// into nested function literals.
func blockHasDefer(n ast.Node) bool {
	found := false
	ast.Inspect(n, func(m ast.Node) bool {
		if found {
			return false
		}
		if _, ok := m.(*ast.FuncLit); ok {
			return false // a nested function's defers are its own
		}
		if _, ok := m.(*ast.DeferStmt); ok {
			found = true
			return false
		}
		return true
	})
	return found
}

// buildClosureDeferred wraps a function-literal body that uses defer in the same
// mark/try/run epilogue as a top-level deferred function. The closure result is a
// single boxed object (or null for void).
func (cl *funcLowerer) buildClosureDeferred(body *ast.BlockStmt) {
	cl.needsInvoker = true
	cl.invokeMethod()
	markLocal := cl.addLocal(nil, goir.TInt64)
	cl.emit(goir.Op{Code: goir.OpDeferMark})
	cl.emit(goir.Op{Code: goir.OpStLoc, Local: markLocal})

	tryStart, handlerStart, handlerEnd := cl.label(), cl.label(), cl.label()
	lNormal, lRecovered, lRet, lRecLeave := cl.label(), cl.label(), cl.label(), cl.label()
	cl.deferMode = true
	cl.deferNormalLabel = lNormal
	cl.resultLocal = cl.addLocal(nil, goir.TObject)
	cl.emit(goir.Op{Code: goir.OpLdNull})
	cl.emit(goir.Op{Code: goir.OpStLoc, Local: cl.resultLocal})

	runDefers := func() {
		cl.emit(goir.Op{Code: goir.OpLdLoc, Local: markLocal})
		cl.emit(goir.Op{Code: goir.OpDeferRun})
	}

	cl.mark(tryStart)
	cl.block(body)
	cl.emit(goir.Op{Code: goir.OpLeave, Label: lNormal})

	cl.mark(handlerStart)
	cl.emit(goir.Op{Code: goir.OpCallSetPanic})
	runDefers()
	cl.emit(goir.Op{Code: goir.OpCallPanicHandled})
	cl.emit(goir.Op{Code: goir.OpBrTrue, Label: lRecLeave})
	cl.emit(goir.Op{Code: goir.OpRethrow})
	cl.mark(lRecLeave)
	cl.emit(goir.Op{Code: goir.OpLeave, Label: lRecovered})
	cl.mark(handlerEnd)

	cl.mark(lNormal)
	runDefers()
	cl.emit(goir.Op{Code: goir.OpBr, Label: lRet})

	cl.mark(lRecovered)
	cl.emit(goir.Op{Code: goir.OpLdNull})
	cl.emit(goir.Op{Code: goir.OpStLoc, Local: cl.resultLocal})

	cl.mark(lRet)
	cl.emit(goir.Op{Code: goir.OpLdLoc, Local: cl.resultLocal})
	cl.emit(goir.Op{Code: goir.OpRet})

	cl.m.EH = append(cl.m.EH, goir.EHClause{
		TryStart: tryStart, TryEnd: handlerStart,
		HandlerStart: handlerStart, HandlerEnd: handlerEnd,
	})
}

// buildDeferredBody lowers a function that uses defer. The defer stack is marked
// on entry; `defer` statements (at any nesting) push thunks at runtime; the marked
// defers run LIFO on both the normal return and the panic-unwind paths.
func (l *funcLowerer) buildDeferredBody(fd *ast.FuncDecl) {
	l.needsInvoker = true
	l.invokeMethod() // the dispatcher runs deferred thunks

	markLocal := l.addLocal(nil, goir.TInt64)
	l.emit(goir.Op{Code: goir.OpDeferMark})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: markLocal})

	tryStart, handlerStart, handlerEnd := l.label(), l.label(), l.label()
	lNormal, lRecovered, lRet := l.label(), l.label(), l.label()
	lRecLeave := l.label()
	l.deferNormalLabel = lNormal

	// Named results live in their own (possibly captured) locals; anonymous
	// results use a scratch local zeroed on the recovered path.
	named := len(l.namedResults) > 0
	l.resultLocal = -1
	if !named && l.m.Ret != goir.TVoid {
		l.resultLocal = l.addLocal(nil, l.m.Ret)
	}

	runDefers := func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: markLocal})
		l.emit(goir.Op{Code: goir.OpDeferRun})
	}

	// try { body; leave normal }
	l.mark(tryStart)
	l.block(fd.Body)
	l.emit(goir.Op{Code: goir.OpLeave, Label: lNormal})

	// catch (GoPanicException) { setPanic; run defers; if handled leave else rethrow }
	l.mark(handlerStart)
	l.emit(goir.Op{Code: goir.OpCallSetPanic}) // consumes the exception on the stack
	runDefers()
	l.emit(goir.Op{Code: goir.OpCallPanicHandled})
	l.emit(goir.Op{Code: goir.OpBrTrue, Label: lRecLeave})
	l.emit(goir.Op{Code: goir.OpRethrow})
	l.mark(lRecLeave)
	l.emit(goir.Op{Code: goir.OpLeave, Label: lRecovered})
	l.mark(handlerEnd)

	// normal path: run defers, return the result.
	l.mark(lNormal)
	runDefers()
	l.emit(goir.Op{Code: goir.OpBr, Label: lRet})

	// recovered path: defers already ran. With named results the deferred call may
	// have set them (the recover-to-error idiom), so keep their values; anonymous
	// results return the zero value.
	l.mark(lRecovered)
	if !named && l.resultLocal >= 0 {
		l.emitZeroValue(l.m.Ret)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: l.resultLocal})
	}

	l.mark(lRet)
	if named {
		l.loadNamedResults()
	} else if l.resultLocal >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: l.resultLocal})
	}
	l.emit(goir.Op{Code: goir.OpRet})

	l.m.EH = append(l.m.EH, goir.EHClause{
		TryStart: tryStart, TryEnd: handlerStart,
		HandlerStart: handlerStart, HandlerEnd: handlerEnd,
	})
}

// deferStmt lowers a defer statement: it evaluates the func value and arguments
// now (Go semantics) into a thunk closure, then pushes it onto the runtime defer
// stack. This works at any nesting (loops, conditionals, blocks).
func (l *funcLowerer) deferStmt(s *ast.DeferStmt) {
	if !l.deferMode {
		l.fail(s.Pos(), "defer")
		return
	}
	l.needsInvoker = true
	l.invokeMethod()
	call := s.Call

	switch fun := call.Fun.(type) {
	case *ast.FuncLit:
		if len(call.Args) == 0 {
			l.closureLit(fun) // GoClosure on the stack
			l.emit(goir.Op{Code: goir.OpDeferPush})
			return
		}
		l.deferFuncValue(call) // func literal called with arguments
		return
	case *ast.SelectorExpr:
		if seln := l.pkg.TypesInfo.Selections[fun]; seln != nil && seln.Kind() == types.MethodVal {
			if ext, ok := l.shimMethodExtern(seln); ok {
				l.deferShimMethod(call, fun, seln, ext)
				return
			}
			// defer of an interface method (direct, or promoted from an embedded
			// interface field such as database/sql's driverStmt{ sync.Locker }).
			if fn, ok := seln.Obj().(*types.Func); ok {
				recvT := l.pkg.TypesInfo.TypeOf(fun.X)
				if iface, ok := recvT.Underlying().(*types.Interface); ok {
					l.deferInterfaceMethod(call, fn, iface, func() { l.expr(fun.X) })
					return
				}
				if iface, ok := fn.Type().(*types.Signature).Recv().Type().Underlying().(*types.Interface); ok {
					if idx := seln.Index(); len(idx) > 1 {
						sel := fun
						l.deferInterfaceMethod(call, fn, iface, func() { l.emitEmbeddedIfaceValue(sel, idx[:len(idx)-1]) })
						return
					}
				}
			}
			l.deferMethod(call, fun, seln)
			return
		}
		// Package-qualified free function: defer pkg.Func(args) — shimmed (e.g.
		// fmt.Println) or compiled. Handle here so variadic args are packed; falling
		// through to deferFuncValue would mis-bind a shim's parameters.
		if fn, ok := l.pkg.TypesInfo.Uses[fun.Sel].(*types.Func); ok {
			if sig, ok := fn.Type().(*types.Signature); ok && sig.Recv() == nil {
				if ext, ok := l.shimExtern(fn); ok {
					l.deferShimCall(call, ext, sig.Variadic())
					return
				}
				if callee, ok := l.byFunc[fn]; ok {
					l.deferNamed(call, callee)
					return
				}
			}
		}
	case *ast.Ident:
		if b, ok := l.pkg.TypesInfo.Uses[fun].(*types.Builtin); ok {
			l.deferBuiltin(call, b.Name())
			return
		}
		if callee, ok := l.byFunc[l.funcObj(fun)]; ok {
			l.deferNamed(call, callee)
			return
		}
	}
	if l.exprType(call.Fun).Kind == goir.KFunc {
		l.deferFuncValue(call)
		return
	}
	l.fail(s.Pos(), "defer of this call form")
}

// deferBuiltin lowers `defer println(args)` / `defer print(args)` /
// `defer close(ch)` by capturing the arguments and re-running the builtin.
func (l *funcLowerer) deferBuiltin(call *ast.CallExpr, name string) {
	switch name {
	case "println", "print":
		captures := make([]thunkCapture, len(call.Args))
		for i, a := range call.Args {
			arg := a
			captures[i] = thunkCapture{emit: func() { l.emitBoxedElem(arg) }, typ: goir.TObject}
		}
		nArgs := len(call.Args)
		isPrintln := name == "println"
		l.buildThunk(captures, func(cl *funcLowerer) {
			cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(nArgs)})
			cl.emit(goir.Op{Code: goir.OpNewObjArray})
			for j := 0; j < nArgs; j++ {
				cl.emit(goir.Op{Code: goir.OpDup})
				cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(j)})
				cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 0})
				cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(j)})
				cl.emit(goir.Op{Code: goir.OpLdElemRef})
				cl.emit(goir.Op{Code: goir.OpStelemRef})
			}
			if isPrintln {
				cl.emit(goir.Op{Code: goir.OpCallPrintln})
			} else {
				cl.emit(goir.Op{Code: goir.OpCallPrint})
			}
		})
		l.emit(goir.Op{Code: goir.OpDeferPush})
	case "close":
		ct := l.exprType(call.Args[0])
		captures := []thunkCapture{{emit: func() { l.expr(call.Args[0]) }, typ: ct}}
		l.buildThunk(captures, func(cl *funcLowerer) {
			cl.emitEnvArg(0, ct)
			cl.emit(goir.Op{Code: goir.OpChanClose})
		})
		l.emit(goir.Op{Code: goir.OpDeferPush})
	default:
		l.fail(call.Pos(), "defer of builtin "+name)
	}
}

// thunkCapture is one env slot of a deferred thunk: a value to evaluate now and
// its type (for boxing).
type thunkCapture struct {
	emit func()
	typ  goir.Type
}

// buildThunk creates a lifted __defer_N(env, args) dispatcher entry whose body is
// emitBody, captures `captures` into the closure env at the defer site, and leaves
// the resulting GoClosure on the stack.
func (l *funcLowerer) buildThunk(captures []thunkCapture, emitBody func(cl *funcLowerer)) {
	id := len(l.closures)
	method := &goir.Method{
		Name:    "__defer_" + itoa(id),
		GoName:  "__defer_" + itoa(id),
		Params:  []goir.Type{goir.TObjectArray, goir.TObjectArray}, // env, args (unused)
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
	cl.emit(goir.Op{Code: goir.OpLdNull})
	cl.emit(goir.Op{Code: goir.OpRet})
	if !cl.ok {
		l.ok = false
	}
}

// emitEnvArg pushes env[i] unboxed to type t (used inside a thunk body).
func (cl *funcLowerer) emitEnvArg(i int, t goir.Type) {
	cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 0})
	cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
	cl.emit(goir.Op{Code: goir.OpLdElemRef})
	cl.emitUnbox(t)
}

// deferNamed lowers `defer f(args)` for a top-level function f.
func (l *funcLowerer) deferNamed(call *ast.CallExpr, callee *goir.Method) {
	captures := make([]thunkCapture, len(call.Args))
	for i, a := range call.Args {
		arg := a
		pt := goir.TObject
		if i < len(callee.Params) {
			pt = callee.Params[i]
		}
		captures[i] = thunkCapture{emit: func() { l.exprCoerced(arg, pt) }, typ: pt}
	}
	l.buildThunk(captures, func(cl *funcLowerer) {
		for i, pt := range callee.Params {
			cl.emitEnvArg(i, pt)
		}
		cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: callee})
		if callee.Ret != goir.TVoid {
			cl.emit(goir.Op{Code: goir.OpPop})
		}
	})
	l.emit(goir.Op{Code: goir.OpDeferPush})
}

// deferMethod lowers `defer recv.Method(args)`, capturing the (adapted) receiver
// and the arguments.
func (l *funcLowerer) deferMethod(call *ast.CallExpr, sel *ast.SelectorExpr, seln *types.Selection) {
	fn, _ := seln.Obj().(*types.Func)
	m := l.byFunc[fn]
	if m == nil {
		l.fail(call.Pos(), "defer of method "+sel.Sel.Name)
		return
	}
	sig := fn.Type().(*types.Signature)
	recvIsPtr := isPointerType(sig.Recv().Type())
	baseType := l.exprType(sel.X)
	baseIsPtr := baseType.Kind == goir.KPtr

	recvEmit := func() {
		switch {
		case recvIsPtr && baseIsPtr:
			l.expr(sel.X)
		case recvIsPtr && !baseIsPtr:
			if !l.emitAddr(sel.X) {
				l.fail(call.Pos(), "defer of pointer-receiver method on a non-addressable value")
			}
		case !recvIsPtr && baseIsPtr:
			l.expr(sel.X)
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emitUnbox(*baseType.Elem)
		default:
			l.expr(sel.X)
		}
	}

	captures := make([]thunkCapture, 0, len(call.Args)+1)
	captures = append(captures, thunkCapture{emit: recvEmit, typ: m.Params[0]})
	for i, a := range call.Args {
		arg := a
		pt := goir.TObject
		if i+1 < len(m.Params) {
			pt = m.Params[i+1]
		}
		captures = append(captures, thunkCapture{emit: func() { l.exprCoerced(arg, pt) }, typ: pt})
	}
	l.buildThunk(captures, func(cl *funcLowerer) {
		for i, pt := range m.Params {
			cl.emitEnvArg(i, pt)
		}
		cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
		if m.Ret != goir.TVoid {
			cl.emit(goir.Op{Code: goir.OpPop})
		}
	})
	l.emit(goir.Op{Code: goir.OpDeferPush})
}

// deferInterfaceMethod lowers `defer recv.Method(args)` where Method is an interface
// method: capture the receiver value and arguments now (Go semantics), then perform
// the interface dispatch at unwind.
func (l *funcLowerer) deferInterfaceMethod(call *ast.CallExpr, fn *types.Func, iface *types.Interface, emitRecv func()) {
	sig := fn.Type().(*types.Signature)
	if sig.Variadic() {
		l.fail(call.Pos(), "defer of a variadic interface method")
		return
	}
	captures := make([]thunkCapture, 0, len(call.Args)+1)
	captures = append(captures, thunkCapture{emit: emitRecv, typ: goir.TObject})
	argTypes := make([]goir.Type, len(call.Args))
	for i, a := range call.Args {
		pt, _ := l.goType(sig.Params().At(i).Type())
		argTypes[i] = pt
		arg := a
		captures = append(captures, thunkCapture{emit: func() { l.exprCoerced(arg, pt) }, typ: pt})
	}
	l.buildThunk(captures, func(cl *funcLowerer) {
		argTmps := make([]int, len(argTypes))
		for i, pt := range argTypes {
			tmp := cl.addLocal(nil, pt)
			cl.emitEnvArg(i+1, pt)
			cl.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
			argTmps[i] = tmp
		}
		ret := cl.interfaceDispatchCore(func() { cl.emitEnvArg(0, goir.TObject) }, fn, iface, argTmps)
		if ret != goir.TVoid {
			cl.emit(goir.Op{Code: goir.OpPop})
		}
	})
	l.emit(goir.Op{Code: goir.OpDeferPush})
}

// deferFuncValue lowers `defer fv(args)` where fv is a function value, capturing
// the closure and arguments and re-dispatching through __invoke at unwind.
func (l *funcLowerer) deferFuncValue(call *ast.CallExpr) {
	l.buildFuncValueThunk(call)
	l.emit(goir.Op{Code: goir.OpDeferPush})
}

// deferShimMethod lowers `defer recv.Method(args)` where Method is a shimmed
// stdlib method: capture the receiver and args, then call the extern at unwind.
func (l *funcLowerer) deferShimMethod(call *ast.CallExpr, sel *ast.SelectorExpr, seln *types.Selection, ext *goir.Extern) {
	captures := make([]thunkCapture, 0, len(call.Args)+1)
	// For a method promoted from an embedded shim field (defer s.Unlock() where s
	// embeds sync.Mutex), capture the embedded field handle, not the outer value.
	recvEmit := func() { l.expr(sel.X) }
	if idx := seln.Index(); len(idx) > 1 {
		recvEmit = func() {
			xt := l.exprType(sel.X)
			l.expr(sel.X)
			l.emitEmbedNav(xt, idx[:len(idx)-1], ext.Params[0])
		}
	}
	captures = append(captures, thunkCapture{emit: recvEmit, typ: ext.Params[0]})
	for i, a := range call.Args {
		arg := a
		pt := goir.TObject
		if i+1 < len(ext.Params) {
			pt = ext.Params[i+1]
		}
		captures = append(captures, thunkCapture{emit: func() { l.exprCoerced(arg, pt) }, typ: pt})
	}
	l.buildThunk(captures, func(cl *funcLowerer) {
		for i, pt := range ext.Params {
			cl.emitEnvArg(i, pt)
		}
		cl.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
		if ext.Ret != goir.TVoid {
			cl.emit(goir.Op{Code: goir.OpPop})
		}
	})
	l.emit(goir.Op{Code: goir.OpDeferPush})
}

// deferShimCall lowers `defer pkg.Func(args)` for a shimmed free function: it
// captures the (eagerly evaluated) arguments — packing the variadic tail into a
// GoSlice, exactly as a direct shim call would — and calls the extern at unwind.
func (l *funcLowerer) deferShimCall(call *ast.CallExpr, ext *goir.Extern, variadic bool) {
	captures := make([]thunkCapture, 0, len(ext.Params))
	if variadic {
		nFixed := len(ext.Params) - 1
		for i := 0; i < nFixed; i++ {
			arg, pt := call.Args[i], ext.Params[i]
			captures = append(captures, thunkCapture{emit: func() { l.exprCoerced(arg, pt) }, typ: pt})
		}
		sliceT := ext.Params[nFixed]
		if call.Ellipsis.IsValid() {
			sa := call.Args[nFixed]
			captures = append(captures, thunkCapture{emit: func() { l.expr(sa) }, typ: sliceT})
		} else {
			tail := call.Args[nFixed:]
			elemT := *sliceT.Elem
			captures = append(captures, thunkCapture{emit: func() { l.packVariadic(tail, elemT) }, typ: sliceT})
		}
	} else {
		for i, pt := range ext.Params {
			arg, p := call.Args[i], pt
			captures = append(captures, thunkCapture{emit: func() { l.exprCoerced(arg, p) }, typ: p})
		}
	}
	l.buildThunk(captures, func(cl *funcLowerer) {
		for i, pt := range ext.Params {
			cl.emitEnvArg(i, pt)
		}
		cl.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
		if ext.Ret != goir.TVoid {
			cl.emit(goir.Op{Code: goir.OpPop})
		}
	})
	l.emit(goir.Op{Code: goir.OpDeferPush})
}

// buildFuncValueThunk builds a zero-arg thunk that invokes a func-value call
// (a func literal or closure variable called with arguments), capturing the
// closure and the evaluated argument values, and leaves the thunk GoClosure on
// the stack. Used by both `defer` and `go`.
func (l *funcLowerer) buildFuncValueThunk(call *ast.CallExpr) {
	captures := make([]thunkCapture, 0, len(call.Args)+1)
	captures = append(captures, thunkCapture{emit: func() { l.expr(call.Fun) }, typ: goir.TFunc})
	for _, a := range call.Args {
		arg := a
		captures = append(captures, thunkCapture{emit: func() { l.emitBoxedElem(arg) }, typ: goir.TObject})
	}
	nArgs := len(call.Args)
	l.buildThunk(captures, func(cl *funcLowerer) {
		cl.emitEnvArg(0, goir.TFunc) // the GoClosure
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(nArgs)})
		cl.emit(goir.Op{Code: goir.OpNewObjArray})
		for j := 0; j < nArgs; j++ {
			cl.emit(goir.Op{Code: goir.OpDup})
			cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(j)})
			cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 0})
			cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(j + 1)})
			cl.emit(goir.Op{Code: goir.OpLdElemRef})
			cl.emit(goir.Op{Code: goir.OpStelemRef})
		}
		cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: cl.invokeMethod()})
		cl.emit(goir.Op{Code: goir.OpPop})
	})
}
