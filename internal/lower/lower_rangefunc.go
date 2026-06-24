package lower

import (
	"go/ast"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// isRangeOverFunc reports whether `range s.X` ranges over a function value
// (Go 1.23 iterators: iter.Seq = func(func(K) bool), iter.Seq2 = func(func(K,V) bool)).
func (c *lowerCtx) isRangeOverFunc(s *ast.RangeStmt) bool {
	t := c.pkg.TypesInfo.TypeOf(s.X)
	if t == nil {
		return false
	}
	_, ok := t.Underlying().(*types.Signature)
	return ok
}

// rangeFuncLit synthesizes a *ast.FuncLit standing in for a range-over-func body:
// its parameters are the range key/value identifiers (so capture analysis treats
// them as bound, not free) and its body is the loop body. Used only to drive
// litFreeVars — the synthetic params carry no type expression, which litFreeVars
// does not need (it keys on the identifiers' Defs).
func rangeFuncLit(s *ast.RangeStmt) *ast.FuncLit {
	var fields []*ast.Field
	for _, kv := range []ast.Expr{s.Key, s.Value} {
		if id, ok := kv.(*ast.Ident); ok && id.Name != "_" {
			fields = append(fields, &ast.Field{Names: []*ast.Ident{id}})
		}
	}
	return &ast.FuncLit{Type: &ast.FuncType{Params: &ast.FieldList{List: fields}}, Body: s.Body}
}

// rangeFuncBodyEscapes reports whether a range-over-func body contains control flow
// that would have to unwind through the iterator call — a return, a defer, or a
// labeled/goto jump. Go implements these with a runtime state machine; goclr does
// not, so such bodies are rejected (honestly) rather than miscompiled. Nested
// function literals are skipped: their returns/defers are their own.
func rangeFuncBodyEscapes(body *ast.BlockStmt) bool {
	found := false
	ast.Inspect(body, func(n ast.Node) bool {
		if found {
			return false
		}
		switch n := n.(type) {
		case *ast.FuncLit:
			return false
		case *ast.ReturnStmt, *ast.DeferStmt:
			found = true
		case *ast.BranchStmt:
			if n.Tok == token.GOTO || n.Label != nil {
				found = true
			}
		}
		return true
	})
	return found
}

// rangeFunc lowers `for k[, v] := range f { body }` where f is an iterator function
// `func(yield func(K[, V]) bool)`. It builds a yield closure from the loop body and
// calls f(yield): the closure binds k/v from yield's arguments, runs the body, and
// returns true to continue or false to stop. A `break` in the body returns false; a
// `continue` (or falling off the end) returns true.
func (l *funcLowerer) rangeFunc(s *ast.RangeStmt) {
	sig, ok := l.pkg.TypesInfo.TypeOf(s.X).Underlying().(*types.Signature)
	if !ok || sig.Params().Len() != 1 {
		l.fail(s.Pos(), "range over func: iterator must take a single yield argument")
		return
	}
	yieldSig, ok := sig.Params().At(0).Type().Underlying().(*types.Signature)
	if !ok || yieldSig.Results().Len() != 1 {
		l.fail(s.Pos(), "range over func: yield must return bool")
		return
	}
	// Only the := form is supported: the key/value are fresh per-iteration variables.
	// The `=` form would assign to existing outer variables each iteration, which the
	// yield-parameter binding does not model.
	if (s.Key != nil || s.Value != nil) && s.Tok != token.DEFINE {
		l.fail(s.Pos(), "range over func with assignment (=) to existing variables is not supported")
		return
	}
	if rangeFuncBodyEscapes(s.Body) {
		l.fail(s.Pos(), "range-over-func body with return/defer/labeled-jump is not supported")
		return
	}
	if _, ok := l.takePendingLoop(); ok {
		l.fail(s.Pos(), "labeled break/continue over a range-over-func is not supported")
		return
	}

	l.needsInvoker = true
	l.invokeMethod()

	captured := l.litFreeVars(rangeFuncLit(s))
	id := len(l.closures)
	method := &goir.Method{
		Name:    "__yield_" + itoa(id),
		GoName:  "__yield_" + itoa(id),
		Params:  []goir.Type{goir.TObjectArray, goir.TObjectArray}, // env, args
		Ret:     goir.TObject,
		Results: []goir.Type{goir.TObject},
	}
	ci := &closureInfo{id: id, method: method, captured: captured}
	l.closures = append(l.closures, ci)
	l.prog.Methods = append(l.prog.Methods, method)
	l.buildYieldClosure(ci, s)

	// At the range site: env = object[]{cells...}; GoClosures.New(id, env) -> temp.
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(id)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(captured))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, cv := range captured {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		idx, ok := l.locals[cv]
		if !ok {
			l.fail(s.Pos(), "captured variable "+cv.Name())
			return
		}
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
	l.emit(goir.Op{Code: goir.OpClosNew})
	yieldTmp := l.addLocal(nil, goir.TFunc)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: yieldTmp})

	// Call f(yield): push f (a function value), pack object[]{yield}, dispatch through
	// __invoke. f returns nothing, so discard the dispatcher's (null) result.
	l.expr(s.X)
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: 1})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	l.emit(goir.Op{Code: goir.OpDup})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: yieldTmp})
	l.emit(goir.Op{Code: goir.OpStelemRef})
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: l.invokeMethod()})
	l.emit(goir.Op{Code: goir.OpPop})
}

// buildYieldClosure lowers the yield closure for a range-over-func: it binds the
// captured cells (from env), binds the range key/value from the yield arguments,
// runs the loop body with break->return-false / continue->return-true, and falls
// off the end as return-true.
func (l *funcLowerer) buildYieldClosure(ci *closureInfo, s *ast.RangeStmt) {
	cl := &funcLowerer{lowerCtx: l.lowerCtx, m: ci.method, ok: true, inClosure: true}
	cl.typeSubst = l.typeSubst
	cl.locals = map[types.Object]int{}
	cl.cells = map[int]goir.Type{}
	cl.addrTaken = cl.analyzeAddrTaken(s.Body)

	// Captured cells come from env (arg 0), same layout as a function literal.
	for i, cv := range ci.captured {
		cvType, _ := cl.goType(cv.Type())
		idx := len(cl.m.Locals)
		cl.m.Locals = append(cl.m.Locals, goir.PtrType(cvType))
		cl.cells[idx] = cvType
		cl.locals[cv] = idx
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 0})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emit(goir.Op{Code: goir.OpUnbox, BoxTy: goir.PtrType(cvType)})
		cl.emit(goir.Op{Code: goir.OpStLoc, Local: idx})
	}

	// The range key/value are the yield parameters, taken from args (arg 1).
	bind := func(kv ast.Expr, j int) {
		id, ok := kv.(*ast.Ident)
		if !ok || id.Name == "_" {
			return
		}
		obj := cl.pkg.TypesInfo.Defs[id]
		if obj == nil {
			return
		}
		pt, _ := cl.goType(obj.Type())
		idx, _ := cl.declareLocal(obj, pt)
		jj := j
		cl.initLocal(idx, func() {
			cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
			cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(jj)})
			cl.emit(goir.Op{Code: goir.OpLdElemRef})
			cl.emitUnbox(pt)
		})
	}
	j := 0
	if s.Key != nil {
		bind(s.Key, j)
		j++
	}
	if s.Value != nil {
		bind(s.Value, j)
	}

	// break -> return false (stop iterating); continue / fall-through -> return true.
	breakLbl := cl.label()
	contLbl := cl.label()
	cl.breaks = append(cl.breaks, breakLbl)
	cl.continues = append(cl.continues, contLbl)
	cl.block(s.Body)
	cl.breaks = cl.breaks[:len(cl.breaks)-1]
	cl.continues = cl.continues[:len(cl.continues)-1]

	cl.mark(contLbl)
	cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 1})
	cl.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TBool})
	cl.emit(goir.Op{Code: goir.OpRet})
	cl.mark(breakLbl)
	cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
	cl.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TBool})
	cl.emit(goir.Op{Code: goir.OpRet})

	if !cl.ok {
		l.ok = false
	}
}
