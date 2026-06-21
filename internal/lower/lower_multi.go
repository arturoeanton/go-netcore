package lower

import (
	"go/ast"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// assignToTarget stores a value (pushed by emitVal) into an LHS identifier,
// handling :=/= and address-taken cells; the blank identifier discards.
func (l *funcLowerer) assignToTarget(s *ast.AssignStmt, lhs ast.Expr, t goir.Type, emitVal func()) {
	// Field target (r.f = <multi-result element>).
	if sel, ok := unparen(lhs).(*ast.SelectorExpr); ok {
		bt := l.exprType(sel.X)
		if bt.Kind == goir.KStruct {
			fi := bt.Struct.FieldIndex(sel.Sel.Name)
			if fi < 0 {
				l.fail(sel.Pos(), "unknown field "+sel.Sel.Name)
				return
			}
			if idx, elem, isCell := l.identCell(sel.X); isCell && elem.Kind == goir.KStruct {
				l.cellFieldModify(idx, elem, fi, func(ft goir.Type) { emitVal() })
				return
			}
			// s[i].field = v : read the boxed struct element, set the field, write back.
			if ix, ok := unparen(sel.X).(*ast.IndexExpr); ok && l.exprType(ix.X).Kind == goir.KSlice {
				xt := l.exprType(ix.X)
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
				emitVal()
				l.emit(goir.Op{Code: goir.OpStFld, Struct: bt.Struct, Field: fi})
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: sliceTmp})
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxTmp})
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: structTmp})
				l.emitBox(bt)
				l.emit(goir.Op{Code: goir.OpSliceSet})
				return
			}
			if l.pointerRootedFieldWriteVal(sel, func(parent *goir.Struct, pfi int) { emitVal() }) {
				return
			}
			l.lvalueAddr(sel.X)
			emitVal()
			l.emit(goir.Op{Code: goir.OpStFld, Struct: bt.Struct, Field: fi})
			return
		}
		if bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KStruct {
			st := *bt.Elem
			fi := st.Struct.FieldIndex(sel.Sel.Name)
			tmp := l.addLocal(nil, st)
			l.expr(sel.X)
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emitUnbox(st)
			l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
			l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
			emitVal()
			l.emit(goir.Op{Code: goir.OpStFld, Struct: st.Struct, Field: fi})
			l.expr(sel.X)
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
			l.emitBox(st)
			l.emit(goir.Op{Code: goir.OpPtrSet})
			return
		}
	}
	// Slice/map element target.
	if ix, ok := unparen(lhs).(*ast.IndexExpr); ok {
		xt := l.exprType(ix.X)
		if xt.Kind == goir.KSlice {
			l.expr(ix.X)
			l.expr(ix.Index)
			emitVal()
			l.emitBox(t)
			l.emit(goir.Op{Code: goir.OpSliceSet})
			return
		}
		if xt.Kind == goir.KMap {
			l.expr(ix.X)
			l.exprCoerced(ix.Index, *xt.Key)
			l.emitBox(*xt.Key)
			emitVal()
			l.emitBox(t)
			l.emit(goir.Op{Code: goir.OpMapSet})
			return
		}
	}
	id, ok := lhs.(*ast.Ident)
	if !ok {
		l.fail(lhs.Pos(), "assignment target")
		return
	}
	if id.Name == "_" {
		return
	}
	if s.Tok == token.DEFINE {
		if obj := l.pkg.TypesInfo.Defs[id]; obj != nil {
			idx, _ := l.declareLocal(obj, t)
			l.initLocal(idx, emitVal)
			return
		}
	}
	// A package-level variable target (e.g. `db, err = sql.Open(...)` where db is a
	// global): store the multi-result element through the global slot.
	if gi, ok := l.globalRef(id); ok && s.Tok != token.DEFINE {
		emitVal()
		l.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(gi)})
		return
	}
	idx, _, ok := l.lookupVar(id)
	if !ok {
		return
	}
	l.assignLocal(idx, emitVal)
}

// multiAssignCall lowers a, b := f() where f returns multiple values (an object[]
// tuple). It stores the tuple in a temp and unpacks each element.
func (l *funcLowerer) multiAssignCall(s *ast.AssignStmt) {
	call := s.Rhs[0].(*ast.CallExpr)
	tup, ok := l.pkg.TypesInfo.TypeOf(call).(*types.Tuple)
	if !ok || tup.Len() != len(s.Lhs) {
		l.fail(s.Pos(), "multiple assignment from call")
		return
	}

	tmp := l.addLocal(nil, goir.TObjectArray)
	l.expr(call) // leaves object[] on the stack
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})

	for i, lhs := range s.Lhs {
		rt, ok := l.goType(tup.At(i).Type())
		if !ok {
			l.fail(lhs.Pos(), "multi-assignment element type")
			return
		}
		// Unbox to the TARGET's type, not the result's: a result element boxed as a
		// concrete type (e.g. strconv.ParseInt's int64) assigned to an interface{} LHS
		// must stay boxed, not be unboxed into the interface slot (which would store the
		// raw bits as a garbage reference). emitUnbox to an interface (KObject) is a
		// no-op, to a concrete type it unboxes.
		target := rt
		if id, isIdent := lhs.(*ast.Ident); !isIdent || id.Name != "_" {
			if lt, ok := l.goType(l.pkg.TypesInfo.TypeOf(lhs)); ok && lt.Kind == goir.KObject {
				target = lt
			}
		}
		idx := i
		l.assignToTarget(s, lhs, target, func() {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
			l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(idx)})
			l.emit(goir.Op{Code: goir.OpLdElemRef})
			l.emitUnbox(target)
		})
	}
}

// parallelAssign lowers a, b = c, d: all right-hand sides are evaluated into
// temps first (Go's simultaneous-assignment semantics), then stored.
func (l *funcLowerer) parallelAssign(s *ast.AssignStmt) {
	n := len(s.Lhs)
	temps := make([]int, n)
	ttypes := make([]goir.Type, n)
	for i, rhs := range s.Rhs {
		// An untyped nil has no type of its own; take the target's type.
		var t goir.Type
		if isNilIdent(rhs) {
			t = l.exprType(s.Lhs[i])
		} else {
			t = l.exprType(rhs)
		}
		ttypes[i] = t
		tmp := l.addLocal(nil, t)
		l.exprCoerced(rhs, t)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
		temps[i] = tmp
	}
	for i, lhs := range s.Lhs {
		tmp := temps[i]
		l.assignToTarget(s, lhs, ttypes[i], func() {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		})
	}
}
