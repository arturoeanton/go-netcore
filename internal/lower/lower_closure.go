package lower

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// closureInfo records a lowered function literal: its lifted static method and
// the variables it captures (in env order).
type closureInfo struct {
	id       int
	method   *goir.Method
	captured []*types.Var
}

// litFreeVars computes the captured variables of a function literal.
func (c *lowerCtx) litFreeVars(lit *ast.FuncLit) []*types.Var {
	defined := map[*types.Var]bool{}
	ast.Inspect(lit, func(n ast.Node) bool {
		if id, ok := n.(*ast.Ident); ok {
			if v, ok := c.pkg.TypesInfo.Defs[id].(*types.Var); ok {
				defined[v] = true
			}
		}
		return true
	})
	var free []*types.Var
	seen := map[*types.Var]bool{}
	ast.Inspect(lit.Body, func(n ast.Node) bool {
		if id, ok := n.(*ast.Ident); ok {
			if v, ok := c.pkg.TypesInfo.Uses[id].(*types.Var); ok && !defined[v] && !seen[v] && !v.IsField() {
				// Only locals (function/block scope, not package-level, not struct
				// fields) become captured cells.
				if v.Parent() != nil && v.Parent() != c.pkg.Types.Scope() {
					seen[v] = true
					free = append(free, v)
				}
			}
		}
		return true
	})
	return free
}

// closureLit lowers a function literal: it lambda-lifts the body to a static
// method and, at the literal site, allocates a GoClosure holding the captured
// cells.
func (l *funcLowerer) closureLit(lit *ast.FuncLit) goir.Type {
	// A closure value may be invoked through the dispatcher (directly, via a
	// goroutine/defer, or after being handed to a stdlib shim like sort.Slice),
	// so ensure the dispatcher exists and is registered at startup.
	l.needsInvoker = true
	l.invokeMethod()
	captured := l.litFreeVars(lit)
	id := len(l.closures)

	method := &goir.Method{
		Name:    "__closure_" + itoa(id),
		GoName:  "__closure_" + itoa(id),
		Params:  []goir.Type{goir.TObjectArray, goir.TObjectArray}, // env, args
		Ret:     goir.TObject,
		Results: []goir.Type{goir.TObject},
	}
	ci := &closureInfo{id: id, method: method, captured: captured}
	l.closures = append(l.closures, ci)
	l.prog.Methods = append(l.prog.Methods, method)

	// Lower the lifted body now.
	l.buildClosure(lit, ci)

	// At the literal site: env = object[]{cell0, cell1, ...}; GoClosures.New(id, env).
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(id)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(captured))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, cv := range captured {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		idx, ok := l.locals[cv]
		if !ok {
			l.fail(lit.Pos(), "captured variable "+cv.Name())
			return goir.TFunc
		}
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx}) // the GoPtr cell
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
	l.emit(goir.Op{Code: goir.OpClosNew})
	return goir.TFunc
}

// buildClosure lowers the lifted body of a function literal.
func (l *funcLowerer) buildClosure(lit *ast.FuncLit, ci *closureInfo) {
	cl := &funcLowerer{lowerCtx: l.lowerCtx, m: ci.method, ok: true, inClosure: true}
	cl.typeSubst = l.typeSubst // inherit generic instantiation context, if any
	cl.locals = map[types.Object]int{}
	cl.cells = map[int]goir.Type{}
	cl.addrTaken = cl.analyzeAddrTaken(lit.Body)

	// Result types: the first (for single-value returns) and the full list (so a
	// multi-result literal returns an object[] tuple).
	cl.closureRet = goir.TVoid
	if res := lit.Type.Results; res != nil && res.NumFields() >= 1 {
		var rts []goir.Type
		for _, f := range res.List {
			ft, _ := cl.goType(cl.pkg.TypesInfo.TypeOf(f.Type))
			n := len(f.Names)
			if n == 0 {
				n = 1
			}
			for k := 0; k < n; k++ {
				rts = append(rts, ft)
			}
		}
		cl.closureRet = rts[0]
		if len(rts) > 1 {
			cl.closureResults = rts
		}
	}

	// Captured cells come from env (arg 0).
	for i, cv := range ci.captured {
		cvType, _ := cl.goType(cv.Type())
		idx := len(cl.m.Locals)
		cl.m.Locals = append(cl.m.Locals, goir.PtrType(cvType))
		cl.cells[idx] = cvType
		cl.locals[cv] = idx
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 0})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(cvType)}) // castclass GoPtr
		cl.emit(goir.Op{Code: goir.OpStLoc, Local: idx})
	}

	// Parameters come from args (arg 1).
	if lit.Type.Params != nil {
		j := 0
		for _, field := range lit.Type.Params.List {
			pt, _ := cl.goType(cl.pkg.TypesInfo.TypeOf(field.Type))
			for _, name := range field.Names {
				obj := cl.pkg.TypesInfo.Defs[name]
				idx, _ := cl.declareLocal(obj, pt)
				jj := j
				cl.initLocal(idx, func() {
					cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
					cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(jj)})
					cl.emit(goir.Op{Code: goir.OpLdElemRef})
					cl.emitUnbox(pt)
				})
				j++
			}
		}
	}

	if blockHasDefer(lit.Body) {
		cl.buildClosureDeferred(lit.Body)
	} else {
		cl.block(lit.Body)
		// Implicit return: lifted methods always return object (null for void).
		cl.emit(goir.Op{Code: goir.OpLdNull})
		cl.emit(goir.Op{Code: goir.OpRet})
	}
	if !cl.ok {
		l.ok = false
	}
}

// funcValueCall lowers f(args) where f is a function value, dispatching through
// the generated __invoke method.
func (l *funcLowerer) funcValueCall(e *ast.CallExpr) goir.Type {
	sig, ok := l.pkg.TypesInfo.TypeOf(e.Fun).Underlying().(*types.Signature)
	if !ok {
		l.fail(e.Pos(), "call of a non-function value")
		return goir.TVoid
	}
	// A multi-result function value returns the same object[] tuple a multi-result
	// named function does; the lifted body produces it and __invoke returns it boxed
	// as object, which the caller (multiAssignCall) unpacks.
	multiResult := sig.Results().Len() > 1
	retType := goir.TVoid
	if sig.Results().Len() == 1 {
		retType, _ = l.goType(sig.Results().At(0).Type())
	} else if multiResult {
		retType = goir.TObjectArray
	}

	l.expr(e.Fun) // GoClosure
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(e.Args))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, a := range e.Args {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		l.emitBoxedElem(a)
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: l.invokeMethod()})
	if retType == goir.TVoid {
		l.emit(goir.Op{Code: goir.OpPop})
		return goir.TVoid
	}
	// Cast the boxed object result to the concrete return type (a value type is
	// unboxed; object[] / reference types are castclass'd via unbox.any).
	l.emitUnbox(retType)
	return retType
}

// isFuncValue reports whether an expression denotes a function VALUE (a closure),
// as opposed to a direct reference to a named function.
func (l *funcLowerer) isFuncValue(e ast.Expr) bool {
	if id, ok := e.(*ast.Ident); ok {
		switch l.pkg.TypesInfo.Uses[id].(type) {
		case *types.Func, *types.Builtin, *types.TypeName, *types.Nil:
			return false // named function, builtin, type conversion, or nil
		}
	}
	t, ok := l.goType(l.pkg.TypesInfo.TypeOf(e))
	return ok && t.Kind == goir.KFunc
}

// invokeMethod returns the shared dispatcher, creating its shell on first use.
func (c *lowerCtx) invokeMethod() *goir.Method {
	if c.invoke == nil {
		c.invoke = &goir.Method{
			Name: "__invoke", GoName: "__invoke",
			Params: []goir.Type{goir.TFunc, goir.TObjectArray}, Ret: goir.TObject,
		}
		c.prog.Methods = append(c.prog.Methods, c.invoke)
	}
	return c.invoke
}

// finishInvoke fills the dispatcher body: switch on the closure id, calling the
// matching lifted method with (env, args).
func (c *lowerCtx) finishInvoke() {
	if c.invoke == nil {
		return
	}
	c.prog.Invoke = c.invoke
	m := c.invoke
	lbl := 0
	next := func() int { lbl++; return lbl }
	for _, ci := range c.closures {
		skip := next()
		m.Code = append(m.Code,
			goir.Op{Code: goir.OpLdArg, Arg: 0},
			goir.Op{Code: goir.OpClosId},
			goir.Op{Code: goir.OpLdcI8, Int: int64(ci.id)},
			goir.Op{Code: goir.OpCeq},
			goir.Op{Code: goir.OpBrFalse, Label: skip},
			goir.Op{Code: goir.OpLdArg, Arg: 0},
			goir.Op{Code: goir.OpClosEnv},
			goir.Op{Code: goir.OpLdArg, Arg: 1},
			goir.Op{Code: goir.OpCallMethod, Callee: ci.method},
			goir.Op{Code: goir.OpRet},
			goir.Op{Code: goir.OpLabel, Label: skip},
		)
	}
	// Fallback: a function value with no lifted Id is a native (runtime/stdlib)
	// closure (e.g. context.CancelFunc); dispatch to its delegate.
	m.Code = append(m.Code,
		goir.Op{Code: goir.OpLdArg, Arg: 0},
		goir.Op{Code: goir.OpLdArg, Arg: 1},
		goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "NativeClosures", Method: "InvokeNative",
			Params: []goir.Type{goir.TFunc, goir.TObjectArray}, Ret: goir.TObject,
		}},
		goir.Op{Code: goir.OpRet})
}

func itoa(n int) string {
	if n == 0 {
		return "0"
	}
	var b []byte
	for n > 0 {
		b = append([]byte{byte('0' + n%10)}, b...)
		n /= 10
	}
	return string(b)
}
