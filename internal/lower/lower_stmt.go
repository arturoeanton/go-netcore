package lower

import (
	"go/ast"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

func (l *funcLowerer) block(b *ast.BlockStmt) {
	for _, s := range b.List {
		if !l.ok {
			return
		}
		l.stmt(s)
	}
}

func (l *funcLowerer) stmt(s ast.Stmt) {
	if !l.ok {
		return
	}
	switch s := s.(type) {
	case *ast.ExprStmt:
		l.exprStmt(s)
	case *ast.AssignStmt:
		l.assign(s)
	case *ast.DeclStmt:
		l.declStmt(s)
	case *ast.IfStmt:
		l.ifStmt(s)
	case *ast.ForStmt:
		l.forStmt(s)
	case *ast.SwitchStmt:
		l.switchStmt(s)
	case *ast.TypeSwitchStmt:
		l.typeSwitch(s)
	case *ast.RangeStmt:
		l.rangeStmt(s)
	case *ast.DeferStmt:
		l.deferStmt(s)
	case *ast.IncDecStmt:
		l.incDec(s)
	case *ast.ReturnStmt:
		l.returnStmt(s)
	case *ast.BranchStmt:
		l.branchStmt(s)
	case *ast.SendStmt:
		l.sendStmt(s)
	case *ast.GoStmt:
		l.goStmt(s)
	case *ast.SelectStmt:
		l.selectStmt(s)
	case *ast.LabeledStmt:
		l.labeledStmt(s)
	case *ast.BlockStmt:
		l.block(s)
	case *ast.EmptyStmt:
		// nothing
	default:
		l.fail(s.Pos(), "statement")
	}
}

func (l *funcLowerer) exprStmt(s *ast.ExprStmt) {
	// A bare receive `<-ch` used as a statement: receive and discard the value.
	if u, ok := s.X.(*ast.UnaryExpr); ok && u.Op == token.ARROW {
		l.expr(u)
		if l.ok {
			l.emit(goir.Op{Code: goir.OpPop})
		}
		return
	}
	call, ok := s.X.(*ast.CallExpr)
	if !ok {
		l.fail(s.Pos(), "expression statement")
		return
	}
	t := l.callExpr(call)
	// Discard a non-void result used as a statement.
	if l.ok && t != goir.TVoid {
		l.emit(goir.Op{Code: goir.OpPop})
	}
}

func (l *funcLowerer) assign(s *ast.AssignStmt) {
	if len(s.Lhs) > 1 {
		switch {
		case len(s.Rhs) == 1:
			switch s.Rhs[0].(type) {
			case *ast.IndexExpr:
				l.commaOk(s) // v, ok := m[k]
			case *ast.TypeAssertExpr:
				l.typeAssertOK(s) // v, ok := x.(T)
			case *ast.CallExpr:
				l.multiAssignCall(s) // a, b := f()
			case *ast.UnaryExpr:
				l.chanRecvOK(s) // v, ok := <-ch
			default:
				l.fail(s.Pos(), "multiple assignment source")
			}
		case len(s.Rhs) == len(s.Lhs):
			l.parallelAssign(s) // a, b = c, d
		default:
			l.fail(s.Pos(), "multiple assignment")
		}
		return
	}
	if len(s.Rhs) != 1 {
		l.fail(s.Pos(), "multiple assignment")
		return
	}

	// Package-level variable write (x = v / x op= v / pkg.Var = v).
	if gi, ok := l.globalRef(s.Lhs[0]); ok && s.Tok != token.DEFINE {
		gt := l.prog.Globals[gi].Type
		if s.Tok == token.ASSIGN {
			l.exprCoerced(s.Rhs[0], gt)
		} else {
			binTok, _ := compoundBinToken(s.Tok)
			l.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
			l.expr(s.Rhs[0])
			l.emitArith(binTok, gt)
		}
		l.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
		return
	}

	// Field assignment: p.f = v.
	if sel, ok := s.Lhs[0].(*ast.SelectorExpr); ok && s.Tok == token.ASSIGN {
		l.fieldAssign(sel, s.Rhs[0])
		return
	}

	// Pointer write: *p = v.
	if star, ok := s.Lhs[0].(*ast.StarExpr); ok && s.Tok == token.ASSIGN {
		l.derefWrite(star, s.Rhs[0])
		return
	}

	// Element assignment: s[i] = v / m[k] = v.
	if ix, ok := s.Lhs[0].(*ast.IndexExpr); ok && s.Tok == token.ASSIGN {
		st := l.exprType(ix.X)
		switch st.Kind {
		case goir.KSlice:
			l.sliceIndexWrite(ix, st, s.Rhs[0])
		case goir.KMap:
			l.mapIndexWrite(ix, st, s.Rhs[0])
		default:
			l.fail(ix.Pos(), "index assignment (only slice and map elements are supported)")
		}
		return
	}

	// Compound assignment to a field / index / pointer target: p.f += e, a[i] -= e, *p += e.
	if _, isIdent := s.Lhs[0].(*ast.Ident); !isIdent && s.Tok != token.ASSIGN && s.Tok != token.DEFINE {
		binTok, ok := compoundBinToken(s.Tok)
		if !ok {
			l.fail(s.Pos(), "assignment operator")
			return
		}
		if !l.compoundAssignLValue(s.Lhs[0], binTok, s.Rhs[0]) {
			l.fail(s.Lhs[0].Pos(), "compound assignment target")
		}
		return
	}

	lhs, ok := s.Lhs[0].(*ast.Ident)
	if !ok {
		l.fail(s.Lhs[0].Pos(), "assignment target")
		return
	}

	rhs := s.Rhs[0]
	switch s.Tok {
	case token.DEFINE:
		obj := l.pkg.TypesInfo.Defs[lhs]
		if obj != nil {
			t, _ := l.goType(obj.Type())
			idx, _ := l.declareLocal(obj, t)
			l.initLocal(idx, func() { l.exprCoerced(rhs, t) })
		} else if useObj := l.pkg.TypesInfo.Uses[lhs]; useObj != nil {
			// Redeclaration in a new scope that reuses an existing local.
			l.assignLocal(l.locals[useObj], func() { l.expr(rhs) })
		}
	case token.ASSIGN:
		if lhs.Name == "_" {
			l.expr(rhs)
			if l.ok {
				l.emit(goir.Op{Code: goir.OpPop})
			}
			return
		}
		idx, vt, ok := l.lookupVar(lhs)
		if !ok {
			return
		}
		l.assignLocal(idx, func() { l.exprCoerced(rhs, vt) })
	default:
		// Compound assignment: x op= e  ->  x = x op e.
		binTok, ok := compoundBinToken(s.Tok)
		if !ok {
			l.fail(s.Pos(), "assignment operator")
			return
		}
		idx, vt, ok := l.lookupVar(lhs)
		if !ok {
			return
		}
		l.assignLocal(idx, func() {
			l.loadVar(idx)
			l.expr(rhs)
			l.emitArith(binTok, vt)
		})
	}
}

// promotedFieldWrite lowers u.f = v where f is promoted through embedded fields.
// Value embeds are navigated by a ldflda chain; a pointer embed is dereferenced
// into a temp whose mutated value is written back through the GoPtr (the pointee
// is shared, so this is observable). An address-taken (cell) base is copied out,
// mutated, and copied back through its cell.
func (l *funcLowerer) promotedFieldWrite(sel *ast.SelectorExpr, bt goir.Type, path []int, rhs ast.Expr) {
	last := path[len(path)-1]
	l.promotedFieldWriteVal(sel, bt, path, func(parent goir.Type) {
		l.exprCoerced(rhs, parent.Struct.Fields[last].Type)
	})
}

// promotedFieldWriteVal is promotedFieldWrite with the stored value supplied by
// emitVal (given the container struct type), enabling both plain writes and
// op-assigns to share the embedded-field navigation + pointer write-back.
func (l *funcLowerer) promotedFieldWriteVal(sel *ast.SelectorExpr, bt goir.Type, path []int, emitVal func(parent goir.Type)) {
	// writeBacks defers the PtrSet for each pointer embed crossed (innermost last).
	type wb struct {
		gp, tmp int
		t       goir.Type
	}
	var backs []wb

	// emitToContainer leaves a managed pointer to the struct that directly holds
	// the final field, after walking path[:last]; baseAddr seeds the chain.
	emitToContainer := func(baseAddr func()) goir.Type {
		baseAddr()
		cur := bt
		for _, idx := range path[:len(path)-1] {
			ft := cur.Struct.Fields[idx].Type
			if ft.Kind == goir.KPtr {
				// Pointer embed: load the GoPtr, deref to a temp, continue in the temp.
				gp := l.addLocal(nil, ft)
				l.emit(goir.Op{Code: goir.OpLdFld, Struct: cur.Struct, Field: idx})
				l.emit(goir.Op{Code: goir.OpStLoc, Local: gp})
				pointee := *ft.Elem
				tmp := l.addLocal(nil, pointee)
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: gp})
				l.emit(goir.Op{Code: goir.OpPtrGet})
				l.emitUnbox(pointee)
				l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
				backs = append(backs, wb{gp: gp, tmp: tmp, t: pointee})
				l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
				cur = pointee
				continue
			}
			l.emit(goir.Op{Code: goir.OpLdFldA, Struct: cur.Struct, Field: idx})
			cur = ft
		}
		return cur
	}

	finish := func() {
		// Write each crossed pointer's temp back through its GoPtr (innermost first).
		for i := len(backs) - 1; i >= 0; i-- {
			b := backs[i]
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: b.gp})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: b.tmp})
			l.emitBox(b.t)
			l.emit(goir.Op{Code: goir.OpPtrSet})
		}
	}

	// Address-taken cell base: copy out, mutate, copy back through the cell.
	if cidx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
		ctmp := l.addLocal(nil, elem)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: cidx})
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(elem)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: ctmp})
		parent := emitToContainer(func() { l.emit(goir.Op{Code: goir.OpLdLocA, Local: ctmp}) })
		last := path[len(path)-1]
		emitVal(parent)
		l.emit(goir.Op{Code: goir.OpStFld, Struct: parent.Struct, Field: last})
		finish()
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: cidx})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: ctmp})
		l.emitBox(elem)
		l.emit(goir.Op{Code: goir.OpPtrSet})
		return
	}

	parent := emitToContainer(func() { l.lvalueAddr(sel.X) })
	last := path[len(path)-1]
	emitVal(parent)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: parent.Struct, Field: last})
	finish()
}

// fieldAssign lowers p.f = v: address of the struct, value, stfld.
func (l *funcLowerer) fieldAssign(sel *ast.SelectorExpr, rhs ast.Expr) {
	bt := l.exprType(sel.X)
	if bt.Kind == goir.KPtr && bt.Elem.Kind == goir.KStruct {
		l.ptrStructFieldWrite(sel, bt, rhs)
		return
	}
	// Field write on an opaque stdlib shim (e.g. url.URL.Path = …) -> setter extern.
	if bt.Kind == goir.KObject && bt.Shim != "" {
		if ext, ok := shimFieldSetExtern(bt.Shim, sel.Sel.Name, l.exprType(rhs)); ok {
			l.expr(sel.X)
			l.exprCoerced(rhs, ext.Params[1])
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
			return
		}
	}
	if bt.Kind != goir.KStruct {
		l.fail(sel.Pos(), "field assignment to non-struct")
		return
	}
	fi := bt.Struct.FieldIndex(sel.Sel.Name)
	if fi < 0 {
		// Promoted field write through embedded fields (u.ID = v).
		if path, ok := l.promotedFieldPath(sel); ok {
			l.promotedFieldWrite(sel, bt, path, rhs)
			return
		}
		l.fail(sel.Pos(), "unknown field "+sel.Sel.Name)
		return
	}
	// A captured/address-taken struct local is a GoPtr cell holding the boxed
	// struct; mutate the field through the cell rather than via ldloca of the cell.
	if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
		l.cellFieldModify(idx, elem, fi, func(ft goir.Type) {
			l.expr(rhs)
		})
		return
	}
	// s[i].field = v : read the boxed struct element, set the field, write it back.
	if ix, ok := unparen(sel.X).(*ast.IndexExpr); ok {
		xt := l.exprType(ix.X)
		if xt.Kind == goir.KSlice {
			sliceTmp := l.addLocal(nil, xt)
			idxTmp := l.addLocal(nil, goir.TInt64)
			structTmp := l.addLocal(nil, bt)
			l.expr(ix.X)
			l.emit(goir.Op{Code: goir.OpStLoc, Local: sliceTmp})
			l.expr(ix.Index)
			l.emit(goir.Op{Code: goir.OpStLoc, Local: idxTmp})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: sliceTmp})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxTmp})
			l.emit(goir.Op{Code: goir.OpSliceGet})
			l.emitUnbox(bt)
			l.emit(goir.Op{Code: goir.OpStLoc, Local: structTmp})
			l.emit(goir.Op{Code: goir.OpLdLocA, Local: structTmp})
			l.expr(rhs)
			l.emit(goir.Op{Code: goir.OpStFld, Struct: bt.Struct, Field: fi})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: sliceTmp})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxTmp})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: structTmp})
			l.emitBox(bt)
			l.emit(goir.Op{Code: goir.OpSliceSet})
			return
		}
	}
	// ptr.Inner.field = v : a field of a value-struct reached through a pointer is
	// not directly addressable (the struct lives in a boxed cell), so read-modify-
	// write the pointer's rooted struct.
	if l.pointerRootedFieldWrite(sel, rhs) {
		return
	}
	// global.field = v : a field of a package-level struct global is reached via
	// ldsfld, not ldloca; RMW through the global.
	if gi, ok := l.globalRef(sel.X); ok {
		l.globalFieldModify(gi, bt, fi, func(ft goir.Type) { l.exprCoerced(rhs, ft) })
		return
	}
	l.lvalueAddr(sel.X)
	l.expr(rhs)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: bt.Struct, Field: fi})
}

// pointerRootedFieldWrite handles assignment to a field reached through a chain of
// value-struct selectors rooted at a *struct (e.g. p.A.B.x = v). It returns false
// if the chain is not so rooted (handled by the normal addressable path).
func (l *funcLowerer) pointerRootedFieldWrite(sel *ast.SelectorExpr, rhs ast.Expr) bool {
	return l.pointerRootedFieldWriteVal(sel, func(parent *goir.Struct, fi int) {
		l.exprCoerced(rhs, parent.Fields[fi].Type)
	})
}

// pointerRootedFieldWriteVal is pointerRootedFieldWrite with the stored value
// supplied by emitVal — to which the container's managed address has already been
// pushed (so a compound op can Dup + LdFld the old value). Returns false if the
// chain is not rooted at a *struct.
func (l *funcLowerer) pointerRootedFieldWriteVal(sel *ast.SelectorExpr, emitVal func(parent *goir.Struct, fi int)) bool {
	var fields []string
	cur := ast.Expr(sel)
	for {
		s, ok := unparen(cur).(*ast.SelectorExpr)
		if !ok {
			return false
		}
		fields = append([]string{s.Sel.Name}, fields...)
		bt := l.exprType(s.X)
		if bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KStruct {
			l.emitPtrRootedWrite(s.X, *bt.Elem, fields, emitVal)
			return true
		}
		if bt.Kind != goir.KStruct {
			return false
		}
		cur = s.X
	}
}

// emitPtrRootedWrite reads *ptr into a temp, leaves the address of the target
// field's container struct on the stack for emitVal (which pushes the value and is
// followed by stfld), then stores the temp back through the pointer.
func (l *funcLowerer) emitPtrRootedWrite(ptrExpr ast.Expr, root goir.Type, fields []string, emitVal func(parent *goir.Struct, fi int)) {
	pt := l.exprType(ptrExpr)
	pTmp := l.addLocal(nil, pt)
	l.expr(ptrExpr)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: pTmp})

	rTmp := l.addLocal(nil, root)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(root)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: rTmp})

	l.emit(goir.Op{Code: goir.OpLdLocA, Local: rTmp})
	cur := root
	for i := 0; i < len(fields)-1; i++ {
		fi := cur.Struct.FieldIndex(fields[i])
		if fi < 0 || cur.Struct.Fields[fi].Type.Kind != goir.KStruct {
			l.fail(ptrExpr.Pos(), "nested field write through a non-struct field")
			return
		}
		l.emit(goir.Op{Code: goir.OpLdFldA, Struct: cur.Struct, Field: fi})
		cur = cur.Struct.Fields[fi].Type
	}
	last := cur.Struct.FieldIndex(fields[len(fields)-1])
	emitVal(cur.Struct, last)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: cur.Struct, Field: last})

	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: rTmp})
	l.emitBox(root)
	l.emit(goir.Op{Code: goir.OpPtrSet})
}

// cellFieldModify performs a read-modify-write of field fi of the struct in cell
// idx: it unboxes the struct, runs writeField (which leaves the new field value
// after a ldflda/dup as needed via emitFieldStore), and reboxes. For a plain set
// the dup-load is skipped; for op= it is included. Here writeField is given the
// struct-address-loaded state and must end having pushed the value to store.
func (l *funcLowerer) cellFieldModify(idx int, st goir.Type, fi int, pushValue func(ft goir.Type)) {
	ft := st.Struct.Fields[fi].Type
	sTmp := l.addLocal(nil, st)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(st)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: sTmp})
	l.emit(goir.Op{Code: goir.OpLdLocA, Local: sTmp})
	pushValue(ft)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idx})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sTmp})
	l.emitBox(st)
	l.emit(goir.Op{Code: goir.OpPtrSet})
}

// globalFieldModify performs a read-modify-write of field fi of a package-level
// struct global gi: load the global into a temp, set the field (pushValue leaves
// the new value after a ldloca), and store the temp back.
func (l *funcLowerer) globalFieldModify(gi int, st goir.Type, fi int, pushValue func(ft goir.Type)) {
	ft := st.Struct.Fields[fi].Type
	sTmp := l.addLocal(nil, st)
	l.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: sTmp})
	l.emit(goir.Op{Code: goir.OpLdLocA, Local: sTmp})
	pushValue(ft)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sTmp})
	l.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
}

// lvalueAddr emits the managed address of an addressable expression (a local or
// a chain of struct field selectors rooted at a local).
func (l *funcLowerer) lvalueAddr(e ast.Expr) {
	switch e := e.(type) {
	case *ast.Ident:
		if idx, _, ok := l.lookupVar(e); ok {
			l.emit(goir.Op{Code: goir.OpLdLocA, Local: idx})
		}
	case *ast.ParenExpr:
		l.lvalueAddr(e.X)
	case *ast.SelectorExpr:
		bt := l.exprType(e.X)
		if bt.Kind != goir.KStruct {
			l.fail(e.Pos(), "addressable field on non-struct")
			return
		}
		fi := bt.Struct.FieldIndex(e.Sel.Name)
		if fi < 0 {
			l.fail(e.Pos(), "unknown field "+e.Sel.Name)
			return
		}
		l.lvalueAddr(e.X)
		l.emit(goir.Op{Code: goir.OpLdFldA, Struct: bt.Struct, Field: fi})
	default:
		l.fail(e.Pos(), "addressable expression")
	}
}

func (l *funcLowerer) declStmt(s *ast.DeclStmt) {
	gd, ok := s.Decl.(*ast.GenDecl)
	if !ok {
		l.fail(s.Pos(), "declaration")
		return
	}
	// Local type declarations are registered in go/types and resolved lazily when
	// used (structFor); local consts are folded at their use sites. Both are no-ops.
	if gd.Tok == token.TYPE || gd.Tok == token.CONST {
		return
	}
	if gd.Tok != token.VAR {
		l.fail(s.Pos(), "declaration")
		return
	}
	for _, spec := range gd.Specs {
		vs := spec.(*ast.ValueSpec)
		for i, name := range vs.Names {
			obj := l.pkg.TypesInfo.Defs[name]
			t, ok := l.goType(obj.Type())
			if !ok {
				l.fail(name.Pos(), "variable type")
				return
			}
			idx, _ := l.declareLocal(obj, t)
			objType := obj.Type()
			if i < len(vs.Values) {
				v := vs.Values[i]
				l.initLocal(idx, func() { l.exprCoerced(v, t) })
			} else if arr, ok := objType.Underlying().(*types.Array); ok {
				// `var a [N]T` zero value is N zeroed elements, not a nil slice.
				l.initLocal(idx, func() { l.emitArrayZero(*t.Elem, arr.Len()) })
			} else {
				l.initLocal(idx, func() { l.emitZeroValue(t) })
			}
		}
	}
}

func (l *funcLowerer) ifStmt(s *ast.IfStmt) {
	if s.Init != nil {
		l.stmt(s.Init)
	}
	elseLbl := l.label()
	endLbl := elseLbl
	if s.Else != nil {
		endLbl = l.label()
	}
	l.expr(s.Cond) // bool on stack
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: elseLbl})
	l.block(s.Body)
	if s.Else != nil {
		l.emit(goir.Op{Code: goir.OpBr, Label: endLbl})
		l.mark(elseLbl)
		switch e := s.Else.(type) {
		case *ast.BlockStmt:
			l.block(e)
		default:
			l.stmt(e) // else if
		}
	}
	l.mark(endLbl)
}

// loopLabels carries a loop's break (end) and continue (post) IR label ids,
// pre-allocated by labeledStmt so labeled break/continue can target them.
type loopLabels struct {
	end  int
	post int
}

// takePendingLoop consumes a pending labeled-loop registration (if any) so the
// loop that follows uses the pre-allocated break/continue labels. It returns
// the labels to use and whether they were pre-allocated.
func (l *funcLowerer) takePendingLoop() (loopLabels, bool) {
	if l.pendingLoopLabel != nil {
		ll := *l.pendingLoopLabel
		l.pendingLoopLabel = nil
		return ll, true
	}
	return loopLabels{}, false
}

// loopExits returns the continue (post) and break (end) IR labels for a loop,
// using labels pre-allocated by labeledStmt when present, otherwise allocating
// fresh ones. Used by the range-loop variants.
func (l *funcLowerer) loopExits() (cont, end int) {
	if ll, ok := l.takePendingLoop(); ok {
		return ll.post, ll.end
	}
	return l.label(), l.label()
}

func (l *funcLowerer) forStmt(s *ast.ForStmt) {
	// Go 1.22: a three-clause for loop whose loop variable is captured by a closure
	// gives each iteration its own instance of the variable. When the variable is
	// address-taken, lower it with a fresh per-iteration cell so closures observe
	// the value at their iteration (not the final value).
	if obj, t, ok := l.capturedLoopVar(s); ok {
		l.forStmtCaptured(s, obj, t)
		return
	}
	pre, havePre := l.takePendingLoop()
	if s.Init != nil {
		l.stmt(s.Init)
	}
	top := l.label()
	post := pre.post
	end := pre.end
	if !havePre {
		post = l.label()
		end = l.label()
	}
	l.mark(top)
	if s.Cond != nil {
		l.expr(s.Cond)
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})
	}
	l.breaks = append(l.breaks, end)
	l.continues = append(l.continues, post)
	l.block(s.Body)
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.continues = l.continues[:len(l.continues)-1]
	l.mark(post)
	if s.Post != nil {
		l.stmt(s.Post)
	}
	l.emit(goir.Op{Code: goir.OpBr, Label: top})
	l.mark(end)
}

// capturedLoopVar reports the loop variable of a three-clause for whose `i := …`
// init declares a single address-taken variable (i.e. captured by a closure in the
// body), for which Go 1.22 per-iteration semantics matter.
func (l *funcLowerer) capturedLoopVar(s *ast.ForStmt) (types.Object, goir.Type, bool) {
	as, ok := s.Init.(*ast.AssignStmt)
	if !ok || as.Tok != token.DEFINE || len(as.Lhs) != 1 || len(as.Rhs) != 1 {
		return nil, goir.Type{}, false
	}
	id, ok := as.Lhs[0].(*ast.Ident)
	if !ok || id.Name == "_" {
		return nil, goir.Type{}, false
	}
	obj := l.pkg.TypesInfo.Defs[id]
	if obj == nil || !l.addrTaken[obj] {
		return nil, goir.Type{}, false
	}
	t, ok := l.goType(obj.Type())
	if !ok {
		return nil, goir.Type{}, false
	}
	return obj, t, true
}

// forStmtCaptured lowers a three-clause for whose loop variable is captured, using
// a plain "carry" local for the condition/post and a fresh GoPtr "capture" cell per
// iteration for the body. The capture cell is re-allocated each iteration so each
// closure binds that iteration's value; modifications in the body are synced back
// to the carry before the post runs.
func (l *funcLowerer) forStmtCaptured(s *ast.ForStmt, obj types.Object, t goir.Type) {
	pre, havePre := l.takePendingLoop()
	carry := l.addLocal(nil, t)
	capture := len(l.m.Locals)
	l.m.Locals = append(l.m.Locals, goir.PtrType(t))
	l.cells[capture] = t

	// init: evaluate the loop variable's initializer into the carry local.
	init := s.Init.(*ast.AssignStmt)
	l.exprCoerced(init.Rhs[0], t)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: carry})

	top := l.label()
	post := l.label()
	// A labeled captured loop must reuse the labels its labeled-statement registered
	// so labeled break/continue resolve here (continue lands on the sync point).
	cont := post
	end := l.label()
	if havePre {
		cont = pre.post
		end = pre.end
	} else {
		cont = l.label()
	}

	l.mark(top)
	// Condition reads the carry value.
	l.locals[obj] = carry
	if s.Cond != nil {
		l.expr(s.Cond)
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})
	}
	// Fresh per-iteration cell initialized from the carry; the body sees it.
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: carry})
	l.emitBox(t)
	l.ptrNew(t)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: capture})
	l.locals[obj] = capture

	l.breaks = append(l.breaks, end)
	l.continues = append(l.continues, cont)
	l.block(s.Body)
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.continues = l.continues[:len(l.continues)-1]

	// Sync any body mutation of the variable back to the carry, then run post.
	l.mark(cont)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: capture})
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(t)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: carry})
	l.mark(post)
	l.locals[obj] = carry
	if s.Post != nil {
		l.stmt(s.Post)
	}
	l.emit(goir.Op{Code: goir.OpBr, Label: top})
	l.mark(end)
}

func (l *funcLowerer) switchStmt(s *ast.SwitchStmt) {
	if s.Init != nil {
		l.stmt(s.Init)
	}
	end := l.label()

	// Evaluate the tag into a local (if present).
	var tagLocal int
	var tagType goir.Type
	hasTag := s.Tag != nil
	if hasTag {
		tagType = l.exprType(s.Tag)
		tagLocal = l.addLocal(nil, tagType)
		l.expr(s.Tag)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: tagLocal})
	}

	type clause struct {
		body *ast.CaseClause
		lbl  int
	}
	var clauses []clause
	defaultLbl := -1
	for _, stmt := range s.Body.List {
		cc := stmt.(*ast.CaseClause)
		lbl := l.label()
		clauses = append(clauses, clause{cc, lbl})
		if cc.List == nil {
			defaultLbl = lbl
		}
	}

	// Dispatch tests.
	for _, c := range clauses {
		if c.body.List == nil {
			continue // default handled last
		}
		for _, e := range c.body.List {
			if hasTag {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: tagLocal})
				l.expr(e)
				l.compare(token.EQL, tagType)
			} else {
				l.expr(e) // boolean case in a tagless switch
			}
			l.emit(goir.Op{Code: goir.OpBrTrue, Label: c.lbl})
		}
	}
	if defaultLbl >= 0 {
		l.emit(goir.Op{Code: goir.OpBr, Label: defaultLbl})
	} else {
		l.emit(goir.Op{Code: goir.OpBr, Label: end})
	}

	// Bodies. Go switch cases break implicitly; a trailing `fallthrough`
	// transfers control into the next clause's body instead of breaking.
	l.breaks = append(l.breaks, end)
	for i, c := range clauses {
		l.mark(c.lbl)
		body := c.body.Body
		falls := false
		// A `fallthrough` is only legal as the final statement of a clause body.
		if n := len(body); n > 0 {
			if bs, ok := body[n-1].(*ast.BranchStmt); ok && bs.Tok == token.FALLTHROUGH {
				if i == len(clauses)-1 {
					l.fail(bs.Pos(), "fallthrough in final case")
					return
				}
				falls = true
				body = body[:n-1] // lower the rest; the fallthrough is the branch
			}
		}
		for _, st := range body {
			l.stmt(st)
		}
		if falls {
			l.emit(goir.Op{Code: goir.OpBr, Label: clauses[i+1].lbl})
		} else {
			l.emit(goir.Op{Code: goir.OpBr, Label: end})
		}
	}
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.mark(end)
}

// rangeStmt lowers `for k, v := range s` over a string: it walks UTF-8 runes,
// exposing the byte index (k, int) and rune (v, int32). An internal iteration
// index drives the loop so the user's k variable matches Go (assigning k in the
// body does not affect iteration).
func (l *funcLowerer) rangeStmt(s *ast.RangeStmt) {
	xt := l.exprType(s.X)
	if xt.Kind == goir.KSlice {
		l.rangeSlice(s, xt)
		return
	}
	if xt.Kind == goir.KMap {
		// rangeMap allocates its own break/continue labels internally and cannot
		// accept pre-allocated ones, so a labeled break/continue over a map range
		// is unsupported. Consume any pending label so it never leaks onto the
		// next loop, and reject the rare labeled-map-range case explicitly.
		if _, ok := l.takePendingLoop(); ok {
			l.fail(s.Pos(), "labeled break/continue over a map range")
			return
		}
		l.rangeMap(s, xt)
		return
	}
	// range over an integer (Go 1.22): for i := range n { ... }, i in 0..n-1.
	if xt.Kind == goir.KInt64 || xt.Kind == goir.KInt32 || xt.Kind == goir.KUint64 || xt.Kind == goir.KUint32 {
		l.rangeInt(s)
		return
	}
	if xt.Kind == goir.KChan {
		l.rangeChan(s, xt)
		return
	}
	if xt.Kind != goir.KString {
		l.fail(s.Pos(), "range (only strings, slices, maps and integers are supported)")
		return
	}

	sLocal := l.addLocal(nil, goir.TString)
	l.expr(s.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: sLocal})

	idxLocal := l.addLocal(nil, goir.TInt64)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})

	keyLocal := l.rangeVar(s, s.Key, goir.TInt64)
	valLocal := l.rangeVar(s, s.Value, goir.TInt32)

	cont, end := l.loopExits()
	top := l.label()
	l.mark(top)
	// idx < len(s) ?
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sLocal})
	l.emit(goir.Op{Code: goir.OpStrLen})
	l.emit(goir.Op{Code: goir.OpClt})
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})

	l.storeRangeVar(keyLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	})
	l.storeRangeVar(valLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: sLocal})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
		l.emit(goir.Op{Code: goir.OpStrRuneAt})
	})

	l.breaks = append(l.breaks, end)
	l.continues = append(l.continues, cont)
	l.block(s.Body)
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.continues = l.continues[:len(l.continues)-1]

	l.mark(cont)
	// idx += runeSize(s, idx)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpStrRuneSize})
	l.emit(goir.Op{Code: goir.OpAdd})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpBr, Label: top})
	l.mark(end)
}

// rangeInt lowers `for i := range n` over an integer n (Go 1.22): i runs 0..n-1.
// n is evaluated once into a temp; an internal index drives the loop so the
// user's i variable matches Go (assigning i in the body does not affect
// iteration). A range over an int has no value, only an optional key (i).
func (l *funcLowerer) rangeInt(s *ast.RangeStmt) {
	if s.Value != nil {
		l.fail(s.Pos(), "range over integer with two variables")
		return
	}

	nLocal := l.addLocal(nil, goir.TInt64)
	l.expr(s.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: nLocal})

	idxLocal := l.addLocal(nil, goir.TInt64)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})

	keyLocal := l.rangeVar(s, s.Key, goir.TInt64)

	cont, end := l.loopExits()
	top := l.label()
	l.mark(top)
	// idx < n ?
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: nLocal})
	l.emit(goir.Op{Code: goir.OpClt})
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})

	l.storeRangeVar(keyLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	})

	l.breaks = append(l.breaks, end)
	l.continues = append(l.continues, cont)
	l.block(s.Body)
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.continues = l.continues[:len(l.continues)-1]

	l.mark(cont)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 1})
	l.emit(goir.Op{Code: goir.OpAdd})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpBr, Label: top})
	l.mark(end)
}

// rangeSlice lowers `for k, v := range s` over a slice: an index loop exposing
// the index (k, int) and the unboxed element (v).
func (l *funcLowerer) rangeSlice(s *ast.RangeStmt, st goir.Type) {
	elem := *st.Elem
	sLocal := l.addLocal(nil, st)
	l.expr(s.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: sLocal})

	idxLocal := l.addLocal(nil, goir.TInt64)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})

	keyLocal := l.rangeVar(s, s.Key, goir.TInt64)
	valLocal := l.rangeVar(s, s.Value, elem)

	cont, end := l.loopExits()
	top := l.label()
	l.mark(top)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: sLocal})
	l.emit(goir.Op{Code: goir.OpSliceLen})
	l.emit(goir.Op{Code: goir.OpClt})
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})

	l.storeRangeVar(keyLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	})
	l.storeRangeVar(valLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: sLocal})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
		l.emit(goir.Op{Code: goir.OpSliceGet})
		l.emitUnbox(elem)
	})

	l.breaks = append(l.breaks, end)
	l.continues = append(l.continues, cont)
	l.block(s.Body)
	l.breaks = l.breaks[:len(l.breaks)-1]
	l.continues = l.continues[:len(l.continues)-1]

	l.mark(cont)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 1})
	l.emit(goir.Op{Code: goir.OpAdd})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpBr, Label: top})
	l.mark(end)
}

// rangeVar resolves a range key/value target to a local, returning -1 for a nil
// or blank (_) target. New variables (`:=`) are declared (becoming a GoPtr cell
// when captured by a closure, so each iteration binds a fresh value per Go 1.22);
// existing ones are reused. Use storeRangeVar to assign the per-iteration value.
func (l *funcLowerer) rangeVar(s *ast.RangeStmt, e ast.Expr, t goir.Type) int {
	id, ok := e.(*ast.Ident)
	if !ok || id.Name == "_" {
		return -1
	}
	if s.Tok == token.DEFINE {
		if obj := l.pkg.TypesInfo.Defs[id]; obj != nil {
			idx, _ := l.declareLocal(obj, t)
			return idx
		}
	}
	idx, _, ok := l.lookupVar(id)
	if !ok {
		return -1
	}
	return idx
}

// storeRangeVar assigns the per-iteration value (pushed by emitValue) to a range
// variable local: for a captured (cell) var it allocates a fresh cell so closures
// observe that iteration's value; for a plain local it stores directly.
func (l *funcLowerer) storeRangeVar(idx int, emitValue func()) {
	if idx < 0 {
		return
	}
	l.initLocal(idx, emitValue)
}

func (l *funcLowerer) incDec(s *ast.IncDecStmt) {
	binTok := token.ADD
	if s.Tok == token.DEC {
		binTok = token.SUB
	}
	if gi, ok := l.globalRef(s.X); ok {
		gt := l.prog.Globals[gi].Type
		l.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		l.emitInt(1, gt)
		l.emitArith(binTok, gt)
		l.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
		return
	}
	switch lhs := s.X.(type) {
	case *ast.Ident:
		idx, t, ok := l.lookupVar(lhs)
		if !ok {
			return
		}
		l.assignLocal(idx, func() {
			l.loadVar(idx)
			l.emitInt(1, t)
			l.emitArith(binTok, t)
		})
	case *ast.SelectorExpr:
		l.modifyField(lhs, binTok, func(ft goir.Type) { l.emitInt(1, ft) })
	case *ast.IndexExpr:
		l.modifyIndex(lhs, binTok, func(et goir.Type) { l.emitInt(1, et) })
	case *ast.StarExpr:
		l.modifyDeref(lhs, binTok, func(et goir.Type) { l.emitInt(1, et) })
	default:
		l.fail(s.Pos(), "increment target")
	}
}

// compoundAssignLValue lowers `lhs op= rhs` for field / index / deref targets
// (the plain-identifier case is handled inline in assign). emitOperand pushes the
// right-hand side coerced to the target's element type.
func (l *funcLowerer) compoundAssignLValue(lhs ast.Expr, binTok token.Token, rhs ast.Expr) bool {
	emit := func(t goir.Type) { l.exprCoerced(rhs, t) }
	switch e := lhs.(type) {
	case *ast.SelectorExpr:
		l.modifyField(e, binTok, emit)
	case *ast.IndexExpr:
		l.modifyIndex(e, binTok, emit)
	case *ast.StarExpr:
		l.modifyDeref(e, binTok, emit)
	default:
		return false
	}
	return true
}

// modifyField lowers s.f OP= rhs / p.f OP= rhs via a read-modify-write on the
// field, where emitOperand pushes the right-hand operand (coerced to the field
// type).
func (l *funcLowerer) modifyField(sel *ast.SelectorExpr, binTok token.Token, emitOperand func(ft goir.Type)) {
	bt := l.exprType(sel.X)
	// Promoted field op-assign (u.ID += v) through embedded fields: navigate to the
	// container (handling cells + pointer embeds via promotedFieldWriteVal) and do
	// the read-modify-write against the duplicated container pointer.
	if bt.Kind == goir.KStruct && bt.Struct.FieldIndex(sel.Sel.Name) < 0 {
		if path, ok := l.promotedFieldPath(sel); ok {
			last := path[len(path)-1]
			l.promotedFieldWriteVal(sel, bt, path, func(parent goir.Type) {
				ft := parent.Struct.Fields[last].Type
				l.emit(goir.Op{Code: goir.OpDup})
				l.emit(goir.Op{Code: goir.OpLdFld, Struct: parent.Struct, Field: last})
				emitOperand(ft)
				l.emitArith(binTok, ft)
			})
			return
		}
	}
	// Captured/address-taken struct local (a GoPtr cell): op= through the cell.
	if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
		fi := elem.Struct.FieldIndex(sel.Sel.Name)
		if fi < 0 {
			l.fail(sel.Pos(), "unknown field "+sel.Sel.Name)
			return
		}
		l.cellFieldModify(idx, elem, fi, func(ft goir.Type) {
			l.emit(goir.Op{Code: goir.OpDup})
			l.emit(goir.Op{Code: goir.OpLdFld, Struct: elem.Struct, Field: fi})
			emitOperand(ft)
			l.emitArith(binTok, ft)
		})
		return
	}
	if bt.Kind == goir.KPtr && bt.Elem.Kind == goir.KStruct {
		st := *bt.Elem
		fi := st.Struct.FieldIndex(sel.Sel.Name)
		if fi < 0 {
			l.fail(sel.Pos(), "unknown field "+sel.Sel.Name)
			return
		}
		ft := st.Struct.Fields[fi].Type
		pTmp := l.addLocal(nil, bt)
		l.expr(sel.X)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: pTmp})
		sTmp := l.addLocal(nil, st)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(st)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: sTmp})
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: sTmp})
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: st.Struct, Field: fi})
		emitOperand(ft)
		l.emitArith(binTok, ft)
		l.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: sTmp})
		l.emitBox(st)
		l.emit(goir.Op{Code: goir.OpPtrSet})
		return
	}
	if bt.Kind != goir.KStruct {
		l.fail(sel.Pos(), "increment of field on non-struct")
		return
	}
	fi := bt.Struct.FieldIndex(sel.Sel.Name)
	if fi < 0 {
		l.fail(sel.Pos(), "unknown field "+sel.Sel.Name)
		return
	}
	ft := bt.Struct.Fields[fi].Type
	// p.A.B.x op= e through a pointer: read-modify-write the rooted struct.
	if l.pointerRootedFieldWriteVal(sel, func(parent *goir.Struct, pfi int) {
		pft := parent.Fields[pfi].Type
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: parent, Field: pfi})
		emitOperand(pft)
		l.emitArith(binTok, pft)
	}) {
		return
	}
	// global.field op= e : RMW through the package-level struct global.
	if gi, ok := l.globalRef(sel.X); ok {
		l.globalFieldModify(gi, bt, fi, func(gft goir.Type) {
			l.emit(goir.Op{Code: goir.OpDup})
			l.emit(goir.Op{Code: goir.OpLdFld, Struct: bt.Struct, Field: fi})
			emitOperand(gft)
			l.emitArith(binTok, gft)
		})
		return
	}
	l.lvalueAddr(sel.X)
	l.emit(goir.Op{Code: goir.OpDup})
	l.emit(goir.Op{Code: goir.OpLdFld, Struct: bt.Struct, Field: fi})
	emitOperand(ft)
	l.emitArith(binTok, ft)
	l.emit(goir.Op{Code: goir.OpStFld, Struct: bt.Struct, Field: fi})
}

// modifyIndex lowers a[i] OP= rhs / m[k] OP= rhs via a read-modify-write.
func (l *funcLowerer) modifyIndex(ix *ast.IndexExpr, binTok token.Token, emitOperand func(et goir.Type)) {
	xt := l.exprType(ix.X)
	switch xt.Kind {
	case goir.KSlice:
		elem := *xt.Elem
		xTmp := l.addLocal(nil, xt)
		iTmp := l.addLocal(nil, goir.TInt64)
		l.expr(ix.X)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: xTmp})
		l.expr(ix.Index)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: xTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: xTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: iTmp})
		l.emit(goir.Op{Code: goir.OpSliceGet})
		l.emitUnbox(elem)
		emitOperand(elem)
		l.emitArith(binTok, elem)
		l.emitBox(elem)
		l.emit(goir.Op{Code: goir.OpSliceSet})
	case goir.KMap:
		valT := *xt.Val
		mTmp := l.addLocal(nil, xt)
		kTmp := l.addLocal(nil, goir.TObject)
		l.expr(ix.X)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: mTmp})
		l.emitBoxedElem(ix.Index)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: kTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: mTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: kTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: mTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: kTmp})
		l.emitBoxedZero(valT)
		l.emit(goir.Op{Code: goir.OpMapGet})
		l.emitUnbox(valT)
		emitOperand(valT)
		l.emitArith(binTok, valT)
		l.emitBox(valT)
		l.emit(goir.Op{Code: goir.OpMapSet})
	default:
		l.fail(ix.Pos(), "increment of index on non-slice/map")
	}
}

// modifyDeref lowers *p OP= rhs via a read-modify-write through the pointer cell.
func (l *funcLowerer) modifyDeref(star *ast.StarExpr, binTok token.Token, emitOperand func(et goir.Type)) {
	pt := l.exprType(star.X)
	if pt.Kind != goir.KPtr {
		l.fail(star.Pos(), "increment through non-pointer")
		return
	}
	elem := *pt.Elem
	pTmp := l.addLocal(nil, pt)
	l.expr(star.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: pTmp})
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(elem)
	emitOperand(elem)
	l.emitArith(binTok, elem)
	l.emitBox(elem)
	l.emit(goir.Op{Code: goir.OpPtrSet})
}

func (l *funcLowerer) returnStmt(s *ast.ReturnStmt) {
	// Inside a lifted closure body: return a boxed value (or null for void). A
	// multi-result literal returns an object[] tuple, which __invoke hands back as
	// object for the caller to unpack — the same shape as a named multi-result fn.
	if l.inClosure {
		switch {
		case len(l.closureResults) > 1:
			// return f() where f yields a matching multi-value tuple: the call already
			// returns a boxed object[] tuple — return it directly.
			if len(s.Results) == 1 {
				tup, ok := l.pkg.TypesInfo.TypeOf(s.Results[0]).(*types.Tuple)
				if !ok || tup.Len() != len(l.closureResults) {
					l.fail(s.Pos(), "naked return in a multi-result closure")
					return
				}
				l.expr(s.Results[0])
			} else if len(s.Results) != len(l.closureResults) {
				l.fail(s.Pos(), "naked return in a multi-result closure")
				return
			} else {
				l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(s.Results))})
				l.emit(goir.Op{Code: goir.OpNewObjArray})
				for i, r := range s.Results {
					l.emit(goir.Op{Code: goir.OpDup})
					l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
					l.emitBoxedElem(r)
					l.emit(goir.Op{Code: goir.OpStelemRef})
				}
			}
		case l.closureRet != goir.TVoid:
			l.emitBoxedElem(s.Results[0])
		default:
			l.emit(goir.Op{Code: goir.OpLdNull})
		}
		// A closure that uses defer routes its return through the defer epilogue.
		if l.deferMode {
			l.emit(goir.Op{Code: goir.OpStLoc, Local: l.resultLocal})
			l.emit(goir.Op{Code: goir.OpLeave, Label: l.deferNormalLabel})
			return
		}
		l.emit(goir.Op{Code: goir.OpRet})
		return
	}

	// Named return values: assign any provided results into their slots, then
	// return them (or, under defer, route through the epilogue that runs defers
	// first so they can observe/modify the named results).
	if len(l.namedResults) > 0 {
		// return f() where f yields a matching multi-value tuple, into named results:
		// spread the boxed tuple across the named slots.
		if len(l.namedResults) > 1 && len(s.Results) == 1 {
			if tup, ok := l.pkg.TypesInfo.TypeOf(s.Results[0]).(*types.Tuple); ok && tup.Len() == len(l.namedResults) {
				l.spreadNamedResults(s.Results[0])
				if l.deferMode {
					l.emit(goir.Op{Code: goir.OpLeave, Label: l.deferNormalLabel})
					return
				}
				l.loadNamedResults()
				l.emit(goir.Op{Code: goir.OpRet})
				return
			}
		}
		if len(s.Results) > 0 {
			l.assignNamedResults(s.Results)
		}
		if l.deferMode {
			l.emit(goir.Op{Code: goir.OpLeave, Label: l.deferNormalLabel})
			return
		}
		l.loadNamedResults()
		l.emit(goir.Op{Code: goir.OpRet})
		return
	}

	// Push the return value onto the stack (nothing for void).
	if len(l.resultTypes) > 1 {
		// return f() where f yields a matching multi-value tuple: the call already
		// returns a boxed object[] tuple, so leave it on the stack as the result.
		if len(s.Results) == 1 {
			tup, ok := l.pkg.TypesInfo.TypeOf(s.Results[0]).(*types.Tuple)
			if !ok || tup.Len() != len(l.resultTypes) {
				l.fail(s.Pos(), "naked multi-return")
				return
			}
			l.expr(s.Results[0])
		} else if len(s.Results) != len(l.resultTypes) {
			l.fail(s.Pos(), "naked multi-return")
			return
		} else {
			l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(s.Results))})
			l.emit(goir.Op{Code: goir.OpNewObjArray})
			for i, r := range s.Results {
				l.emit(goir.Op{Code: goir.OpDup})
				l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
				// Each slot holds a boxed value; box by the expression's own type (a
				// concrete value returned as an interface must still be boxed; a nil
				// interface result becomes null).
				l.emitBoxedElem(r)
				l.emit(goir.Op{Code: goir.OpStelemRef})
			}
		}
	} else if len(s.Results) == 1 {
		l.exprCoerced(s.Results[0], l.resultTypes[0])
	}

	// In a deferred function, returns route through the defer-running epilogue.
	if l.deferMode {
		if l.resultLocal >= 0 {
			l.emit(goir.Op{Code: goir.OpStLoc, Local: l.resultLocal})
		}
		l.emit(goir.Op{Code: goir.OpLeave, Label: l.deferNormalLabel})
		return
	}
	l.emit(goir.Op{Code: goir.OpRet})
}

// assignNamedResults stores each provided return expression into its named
// result slot (cell-aware), coerced to the result type.
func (l *funcLowerer) assignNamedResults(results []ast.Expr) {
	for i, r := range results {
		if i >= len(l.namedResults) || l.namedResults[i] < 0 {
			l.expr(r)
			l.emit(goir.Op{Code: goir.OpPop})
			continue
		}
		idx := l.namedResults[i]
		rt := l.resultTypes[i]
		l.assignLocal(idx, func() { l.exprCoerced(r, rt) })
	}
}

// spreadNamedResults unpacks a multi-result call's boxed object[] tuple across the
// function's named result slots (cell-aware).
func (l *funcLowerer) spreadNamedResults(call ast.Expr) {
	tmp := l.addLocal(nil, goir.TObjectArray)
	l.expr(call)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
	for i := range l.namedResults {
		if l.namedResults[i] < 0 {
			continue
		}
		idx, rt, slot := i, l.resultTypes[i], l.namedResults[i]
		l.assignLocal(slot, func() {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
			l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(idx)})
			l.emit(goir.Op{Code: goir.OpLdElemRef})
			l.emitUnbox(rt)
		})
	}
}

// loadNamedResults pushes the function's return value(s) from the named result
// slots: a single value directly, or a boxed object[] tuple for multiple.
func (l *funcLowerer) loadNamedResults() {
	if len(l.namedResults) == 1 {
		if l.namedResults[0] >= 0 {
			l.loadVar(l.namedResults[0])
		} else {
			l.emitZeroValue(l.resultTypes[0])
		}
		return
	}
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(l.namedResults))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, idx := range l.namedResults {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		if idx >= 0 {
			l.loadVar(idx)
		} else {
			l.emitZeroValue(l.resultTypes[i])
		}
		l.emitBox(l.resultTypes[i])
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
}

func (l *funcLowerer) branchStmt(s *ast.BranchStmt) {
	switch s.Tok {
	case token.GOTO:
		if s.Label == nil {
			l.fail(s.Pos(), "goto without label")
			return
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: l.gotoLabel(s.Label.Name)})
	case token.BREAK:
		if s.Label != nil {
			lbl, ok := l.labeledBreaks[s.Label.Name]
			if !ok {
				l.fail(s.Pos(), "break label "+s.Label.Name+" (only labeled loops are supported)")
				return
			}
			l.emit(goir.Op{Code: goir.OpBr, Label: lbl})
			return
		}
		if len(l.breaks) == 0 {
			l.fail(s.Pos(), "break outside loop/switch")
			return
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: l.breaks[len(l.breaks)-1]})
	case token.CONTINUE:
		if s.Label != nil {
			lbl, ok := l.labeledContinues[s.Label.Name]
			if !ok {
				l.fail(s.Pos(), "continue label "+s.Label.Name+" (only labeled loops are supported)")
				return
			}
			l.emit(goir.Op{Code: goir.OpBr, Label: lbl})
			return
		}
		if len(l.continues) == 0 {
			l.fail(s.Pos(), "continue outside loop")
			return
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: l.continues[len(l.continues)-1]})
	default:
		l.fail(s.Pos(), s.Tok.String()) // fallthrough outside a switch clause
	}
}

// gotoLabel returns the IR label id bound to a Go label name, allocating it on
// first reference so forward gotos (referenced before the label is marked)
// resolve to the same id.
func (l *funcLowerer) gotoLabel(name string) int {
	if id, ok := l.gotoLabels[name]; ok {
		return id
	}
	id := l.label()
	l.gotoLabels[name] = id
	return id
}

// labeledStmt lowers `L: stmt`. It marks L's IR label (so `goto L` lands here),
// then lowers the inner statement. When the inner statement is a for/range loop,
// it pre-allocates the loop's break/continue labels and registers them under L
// so `break L` / `continue L` resolve, then hands them to the loop via
// pendingLoopLabel.
func (l *funcLowerer) labeledStmt(s *ast.LabeledStmt) {
	// Mark the goto target. Stack is empty at a statement boundary.
	l.mark(l.gotoLabel(s.Label.Name))

	if l.isLoop(s.Stmt) {
		ll := loopLabels{end: l.label(), post: l.label()}
		name := s.Label.Name
		prevBreak, hadBreak := l.labeledBreaks[name]
		prevCont, hadCont := l.labeledContinues[name]
		l.labeledBreaks[name] = ll.end
		l.labeledContinues[name] = ll.post
		l.pendingLoopLabel = &ll
		l.stmt(s.Stmt)
		// Restore any shadowed registration (defensive; Go forbids duplicate
		// labels in a function, so this normally just deletes).
		if hadBreak {
			l.labeledBreaks[name] = prevBreak
		} else {
			delete(l.labeledBreaks, name)
		}
		if hadCont {
			l.labeledContinues[name] = prevCont
		} else {
			delete(l.labeledContinues, name)
		}
		return
	}
	l.stmt(s.Stmt)
}

func (l *funcLowerer) isLoop(s ast.Stmt) bool {
	switch s.(type) {
	case *ast.ForStmt, *ast.RangeStmt:
		return true
	default:
		return false
	}
}

// lookupVar resolves an identifier to its local index and type.
func (l *funcLowerer) lookupVar(id *ast.Ident) (int, goir.Type, bool) {
	obj := l.pkg.TypesInfo.ObjectOf(id)
	if obj == nil {
		l.fail(id.Pos(), "undefined "+id.Name)
		return 0, goir.Type{}, false
	}
	idx, ok := l.locals[obj]
	if !ok {
		l.fail(id.Pos(), "variable "+id.Name+" (only function-local int/bool variables are supported)")
		return 0, goir.Type{}, false
	}
	t, _ := l.goType(obj.Type())
	return idx, t, true
}

// compoundBinToken maps a compound-assignment token (x += y) to its binary
// operator token (+), so the operand-type-directed emitArith can pick the right
// opcode (string concat, complex, unsigned, etc.).
func compoundBinToken(tok token.Token) (token.Token, bool) {
	switch tok {
	case token.ADD_ASSIGN:
		return token.ADD, true
	case token.SUB_ASSIGN:
		return token.SUB, true
	case token.MUL_ASSIGN:
		return token.MUL, true
	case token.QUO_ASSIGN:
		return token.QUO, true
	case token.REM_ASSIGN:
		return token.REM, true
	case token.AND_ASSIGN:
		return token.AND, true
	case token.OR_ASSIGN:
		return token.OR, true
	case token.XOR_ASSIGN:
		return token.XOR, true
	case token.SHL_ASSIGN:
		return token.SHL, true
	case token.SHR_ASSIGN:
		return token.SHR, true
	case token.AND_NOT_ASSIGN:
		return token.AND_NOT, true
	default:
		return token.ILLEGAL, false
	}
}
