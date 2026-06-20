package lower

import (
	"go/ast"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// emitRecvUnbox unboxes a received value, yielding the element's zero value when
// the boxed value is null (a receive from a closed, drained channel gives the
// zero value in Go). Reference-type elements unbox directly since null is valid.
func (l *funcLowerer) emitRecvUnbox(elem goir.Type) {
	if !isValueType(elem) {
		l.emitUnbox(elem)
		return
	}
	notNull := l.label()
	done := l.label()
	l.emit(goir.Op{Code: goir.OpDup})
	l.emit(goir.Op{Code: goir.OpBrTrue, Label: notNull})
	l.emit(goir.Op{Code: goir.OpPop})
	l.emitZeroValue(elem)
	l.emit(goir.Op{Code: goir.OpBr, Label: done})
	l.mark(notNull)
	l.emitUnbox(elem)
	l.mark(done)
}

// sendStmt lowers `ch <- v`: the value is coerced to the channel's element type
// and boxed, then handed to GoChans.Send.
func (l *funcLowerer) sendStmt(s *ast.SendStmt) {
	ct := l.exprType(s.Chan)
	if ct.Kind != goir.KChan {
		l.fail(s.Pos(), "send to non-channel")
		return
	}
	l.expr(s.Chan)
	l.exprCoerced(s.Value, *ct.Elem) // box to object if the element is an interface
	l.emitBox(*ct.Elem)              // box value types; no-op for already-boxed objects
	l.emit(goir.Op{Code: goir.OpChanSend})
}

// chanRecvOK lowers `v, ok := <-ch`: GoChans.Recv2 returns object[]{value, ok}.
func (l *funcLowerer) chanRecvOK(s *ast.AssignStmt) {
	recv, ok := s.Rhs[0].(*ast.UnaryExpr)
	if !ok || recv.Op != token.ARROW {
		l.fail(s.Pos(), "multiple assignment source")
		return
	}
	ct := l.exprType(recv.X)
	if ct.Kind != goir.KChan {
		l.fail(s.Pos(), "receive from non-channel")
		return
	}
	elem := *ct.Elem
	tmp := l.addLocal(nil, goir.TObjectArray)
	l.expr(recv.X)
	l.emit(goir.Op{Code: goir.OpChanRecv2})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})

	l.assignToTarget(s, s.Lhs[0], elem, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		l.emit(goir.Op{Code: goir.OpLdElemRef})
		l.emitRecvUnbox(elem)
	})
	l.assignToTarget(s, s.Lhs[1], goir.TBool, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 1})
		l.emit(goir.Op{Code: goir.OpLdElemRef})
		l.emitUnbox(goir.TBool)
	})
}

// rangeChan lowers `for v := range ch`: receive until the channel is closed and
// drained (ok == false), binding each received value to v.
func (l *funcLowerer) rangeChan(s *ast.RangeStmt, ct goir.Type) {
	if s.Value != nil {
		l.fail(s.Pos(), "range over channel with two variables")
		return
	}
	elem := *ct.Elem
	chLocal := l.addLocal(nil, ct)
	l.expr(s.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: chLocal})

	tupLocal := l.addLocal(nil, goir.TObjectArray)
	valLocal := l.rangeVar(s, s.Key, elem)

	cont, end := l.loopExits()
	top := l.label()
	l.mark(top)
	// tup = Recv2(ch); if !tup[1] (ok) break.
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: chLocal})
	l.emit(goir.Op{Code: goir.OpChanRecv2})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tupLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tupLocal})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: 1})
	l.emit(goir.Op{Code: goir.OpLdElemRef})
	l.emitUnbox(goir.TBool)
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})

	if valLocal >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tupLocal})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		l.emit(goir.Op{Code: goir.OpLdElemRef})
		l.emitUnbox(elem)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: valLocal})
	}

	l.breaks = append(l.breaks, end)
	l.continues = append(l.continues, cont)
	l.block(s.Body)
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.continues = l.continues[:len(l.continues)-1]

	l.mark(cont)
	l.emit(goir.Op{Code: goir.OpBr, Label: top})
	l.mark(end)
}

// commChanExpr returns the channel operand of a select comm clause.
func commChanExpr(comm ast.Stmt) ast.Expr {
	switch c := comm.(type) {
	case *ast.SendStmt:
		return c.Chan
	case *ast.ExprStmt:
		if u, ok := c.X.(*ast.UnaryExpr); ok && u.Op == token.ARROW {
			return u.X
		}
	case *ast.AssignStmt:
		if u, ok := c.Rhs[0].(*ast.UnaryExpr); ok && u.Op == token.ARROW {
			return u.X
		}
	}
	return nil
}

// selectStmt lowers a `select` by handing all cases to the GoSelect runtime,
// which returns the chosen case index (and, for a receive, the value/ok). A
// switch on that index runs the corresponding clause body.
func (l *funcLowerer) selectStmt(s *ast.SelectStmt) {
	var cases []*ast.CommClause
	var def *ast.CommClause
	for _, st := range s.Body.List {
		cc, ok := st.(*ast.CommClause)
		if !ok {
			l.fail(st.Pos(), "select clause")
			return
		}
		if cc.Comm == nil {
			def = cc
		} else {
			cases = append(cases, cc)
		}
	}
	n := len(cases)

	resultLocal := l.addLocal(nil, goir.TObjectArray)
	idxLocal := l.addLocal(nil, goir.TInt64)

	buildArray := func(emitElem func(i int)) {
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(n)})
		l.emit(goir.Op{Code: goir.OpNewObjArray})
		for i := 0; i < n; i++ {
			l.emit(goir.Op{Code: goir.OpDup})
			l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
			emitElem(i)
			l.emit(goir.Op{Code: goir.OpStelemRef})
		}
	}

	// chans[i]: the channel (a reference type, stored directly).
	buildArray(func(i int) { l.expr(commChanExpr(cases[i].Comm)) })
	// ops[i]: 0 = recv, 1 = send (boxed int).
	buildArray(func(i int) {
		op := int64(0)
		if _, ok := cases[i].Comm.(*ast.SendStmt); ok {
			op = 1
		}
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: op})
		l.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TInt32})
	})
	// sendVals[i]: the boxed value for a send case, else null.
	buildArray(func(i int) {
		if send, ok := cases[i].Comm.(*ast.SendStmt); ok {
			ct := l.exprType(send.Chan)
			l.exprCoerced(send.Value, *ct.Elem)
			l.emitBox(*ct.Elem)
		} else {
			l.emit(goir.Op{Code: goir.OpLdNull})
		}
	})
	// hasDefault.
	hasDefault := int64(0)
	if def != nil {
		hasDefault = 1
	}
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: hasDefault})
	l.emit(goir.Op{Code: goir.OpSelect})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: resultLocal})

	// idx = (int)result[0].
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: resultLocal})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
	l.emit(goir.Op{Code: goir.OpLdElemRef})
	l.emitUnbox(goir.TInt64)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})

	end := l.label()
	l.breaks = append(l.breaks, end) // `break` exits the select
	for i, cc := range cases {
		next := l.label()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(i)})
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: next})
		l.bindSelectRecv(cc.Comm, resultLocal)
		for _, st := range cc.Body {
			l.stmt(st)
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
		l.mark(next)
	}
	if def != nil {
		for _, st := range def.Body {
			l.stmt(st)
		}
	}
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.mark(end)
}

// bindSelectRecv binds the variables of a `v := <-ch` / `v, ok := <-ch` select
// case from the runtime result (result[1] = value, result[2] = ok).
func (l *funcLowerer) bindSelectRecv(comm ast.Stmt, resultLocal int) {
	assign, ok := comm.(*ast.AssignStmt)
	if !ok {
		return // send case or value-less receive
	}
	recv := assign.Rhs[0].(*ast.UnaryExpr)
	elem := *l.exprType(recv.X).Elem
	l.assignToTarget(assign, assign.Lhs[0], elem, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: resultLocal})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 1})
		l.emit(goir.Op{Code: goir.OpLdElemRef})
		l.emitRecvUnbox(elem)
	})
	if len(assign.Lhs) == 2 {
		l.assignToTarget(assign, assign.Lhs[1], goir.TBool, func() {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: resultLocal})
			l.emit(goir.Op{Code: goir.OpLdcI4, Int: 2})
			l.emit(goir.Op{Code: goir.OpLdElemRef})
			l.emitUnbox(goir.TBool)
		})
	}
}

// goStmt lowers `go f(args)` and `go func(){...}()`. The goroutine body becomes a
// GoClosure dispatched on a background task via GoRuntime.Go. Arguments are
// evaluated in the current goroutine (Go semantics) and captured by value.
func (l *funcLowerer) goStmt(s *ast.GoStmt) {
	l.needsInvoker = true
	// Goroutine bodies are dispatched through the shared closure dispatcher; make
	// sure it exists so GoRuntime.SetInvoker has a valid method to point at.
	l.invokeMethod()
	call := s.Call

	// go func(){ body }() — no args: start the literal's closure directly.
	if lit, ok := call.Fun.(*ast.FuncLit); ok && len(call.Args) == 0 {
		l.closureLit(lit) // leaves a GoClosure on the stack
		l.emit(goir.Op{Code: goir.OpGoStart})
		return
	}
	// go func(a){...}(x) / go closureVar(args) — capture the func value + args in
	// a thunk and start it.
	if _, isLit := call.Fun.(*ast.FuncLit); isLit || l.exprType(call.Fun).Kind == goir.KFunc {
		l.buildFuncValueThunk(call)
		l.emit(goir.Op{Code: goir.OpGoStart})
		return
	}

	fun, ok := call.Fun.(*ast.Ident)
	if !ok {
		l.fail(s.Pos(), "go statement (only named functions and func literals)")
		return
	}
	callee, ok := l.byFunc[l.funcObj(fun)]
	if !ok {
		l.fail(s.Pos(), "go call to "+fun.Name)
		return
	}
	if len(call.Args) != len(callee.Params) {
		l.fail(s.Pos(), "go call with a variadic function")
		return
	}
	l.buildGoThunk(call, callee)
}

// buildGoThunk generates a zero-argument thunk method for `go f(args)`: the
// evaluated argument values are captured in the closure env, and the thunk
// unboxes them and calls f.
func (l *funcLowerer) buildGoThunk(call *ast.CallExpr, callee *goir.Method) {
	id := len(l.closures)
	method := &goir.Method{
		Name:    "__goroutine_" + itoa(id),
		GoName:  "__goroutine_" + itoa(id),
		Params:  []goir.Type{goir.TObjectArray, goir.TObjectArray}, // env, args (unused)
		Ret:     goir.TObject,
		Results: []goir.Type{goir.TObject},
	}
	ci := &closureInfo{id: id, method: method}
	l.closures = append(l.closures, ci)
	l.prog.Methods = append(l.prog.Methods, method)

	// Call site: env = object[]{boxed args}; GoClosures.New(id, env); GoRuntime.Go.
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(id)})
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(call.Args))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, a := range call.Args {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		l.exprCoerced(a, callee.Params[i])
		l.emitBox(callee.Params[i])
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
	l.emit(goir.Op{Code: goir.OpClosNew})
	l.emit(goir.Op{Code: goir.OpGoStart})

	// Thunk body: unbox env[i] -> callee.Params[i]; call f; return null.
	cl := &funcLowerer{lowerCtx: l.lowerCtx, m: method, ok: true, inClosure: true}
	cl.typeSubst = l.typeSubst
	cl.locals = map[types.Object]int{}
	cl.cells = map[int]goir.Type{}
	for i, pt := range callee.Params {
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 0}) // env
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(pt)
	}
	cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: callee})
	if callee.Ret != goir.TVoid {
		cl.emit(goir.Op{Code: goir.OpPop})
	}
	cl.emit(goir.Op{Code: goir.OpLdNull})
	cl.emit(goir.Op{Code: goir.OpRet})
	if !cl.ok {
		l.ok = false
	}
}
