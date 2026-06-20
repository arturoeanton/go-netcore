package lower

import (
	"go/ast"
	"go/token"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// mapLit lowers a map literal map[K]V{k: v, ...}.
func (l *funcLowerer) mapLit(e *ast.CompositeLit, mt goir.Type) goir.Type {
	tmp := l.addLocal(nil, mt)
	l.emit(goir.Op{Code: goir.OpMapMake})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
	for _, elt := range e.Elts {
		kv, ok := elt.(*ast.KeyValueExpr)
		if !ok {
			l.fail(elt.Pos(), "map literal element")
			return goir.TVoid
		}
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emitBoxedElem(kv.Key)
		l.emitBoxedElem(kv.Value)
		l.emit(goir.Op{Code: goir.OpMapSet})
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	return mt
}

// mapIndexRead lowers m[k] where m is a map, leaving the value (zero if absent).
func (l *funcLowerer) mapIndexRead(e *ast.IndexExpr, mt goir.Type) {
	valT := *mt.Val
	l.expr(e.X)
	l.emitBoxedElem(e.Index)
	l.emitBoxedZero(valT)
	l.emit(goir.Op{Code: goir.OpMapGet})
	l.emitUnbox(valT)
}

// mapIndexWrite lowers m[k] = v.
func (l *funcLowerer) mapIndexWrite(e *ast.IndexExpr, mt goir.Type, rhs ast.Expr) {
	l.expr(e.X)
	l.emitBoxedElem(e.Index)
	l.emitBoxedElem(rhs)
	l.emit(goir.Op{Code: goir.OpMapSet})
}

// deleteCall lowers delete(m, k).
func (l *funcLowerer) deleteCall(e *ast.CallExpr) goir.Type {
	if len(e.Args) != 2 {
		l.fail(e.Pos(), "delete")
		return goir.TVoid
	}
	mt := l.exprType(e.Args[0])
	if mt.Kind != goir.KMap {
		l.fail(e.Pos(), "delete on non-map")
		return goir.TVoid
	}
	l.expr(e.Args[0])
	l.emitBoxedElem(e.Args[1])
	l.emit(goir.Op{Code: goir.OpMapDelete})
	return goir.TVoid
}

// commaOk lowers `v, ok := m[k]` (and the `=` form).
func (l *funcLowerer) commaOk(s *ast.AssignStmt) {
	ix := s.Rhs[0].(*ast.IndexExpr)
	mt := l.exprType(ix.X)
	if mt.Kind != goir.KMap {
		l.fail(s.Pos(), "comma-ok on non-map")
		return
	}
	valT := *mt.Val

	mTmp := l.addLocal(nil, mt)
	l.expr(ix.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: mTmp})
	kTmp := l.addLocal(nil, goir.TObject)
	l.emitBoxedElem(ix.Index)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: kTmp})

	vIdx := l.assignTarget(s, s.Lhs[0], valT)
	okIdx := l.assignTarget(s, s.Lhs[1], goir.TBool)

	if vIdx >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: mTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: kTmp})
		l.emitBoxedZero(valT)
		l.emit(goir.Op{Code: goir.OpMapGet})
		l.emitUnbox(valT)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: vIdx})
	}
	if okIdx >= 0 {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: mTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: kTmp})
		l.emit(goir.Op{Code: goir.OpMapContains})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: okIdx})
	}
}

// assignTarget resolves an assignment LHS to a local index (defining for `:=`),
// returning -1 for the blank identifier.
func (l *funcLowerer) assignTarget(s *ast.AssignStmt, e ast.Expr, t goir.Type) int {
	id, ok := e.(*ast.Ident)
	if !ok || id.Name == "_" {
		return -1
	}
	if s.Tok == token.DEFINE {
		if obj := l.pkg.TypesInfo.Defs[id]; obj != nil {
			return l.addLocal(obj, t)
		}
	}
	idx, _, ok := l.lookupVar(id)
	if !ok {
		return -1
	}
	return idx
}

// rangeMap lowers `for k, v := range m`: iterate the map's keys, fetching each
// value. Iteration order is unspecified, as in Go.
func (l *funcLowerer) rangeMap(s *ast.RangeStmt, mt goir.Type) {
	keyT, valT := *mt.Key, *mt.Val
	mTmp := l.addLocal(nil, mt)
	l.expr(s.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: mTmp})

	keysTmp := l.addLocal(nil, goir.SliceType(goir.TObject))
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: mTmp})
	l.emit(goir.Op{Code: goir.OpMapKeys})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: keysTmp})

	idxLocal := l.addLocal(nil, goir.TInt64)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: idxLocal})

	rawKey := l.addLocal(nil, goir.TObject)
	keyLocal := l.rangeVar(s, s.Key, keyT)
	valLocal := l.rangeVar(s, s.Value, valT)

	top, cont, end := l.label(), l.label(), l.label()
	l.mark(top)
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: keysTmp})
	l.emit(goir.Op{Code: goir.OpSliceLen})
	l.emit(goir.Op{Code: goir.OpClt})
	l.emit(goir.Op{Code: goir.OpBrFalse, Label: end})

	// rawKey = keys[idx] (already a boxed key object).
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: keysTmp})
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: idxLocal})
	l.emit(goir.Op{Code: goir.OpSliceGet})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: rawKey})

	l.storeRangeVar(keyLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: rawKey})
		l.emitUnbox(keyT)
	})
	l.storeRangeVar(valLocal, func() {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: mTmp})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: rawKey})
		l.emitBoxedZero(valT)
		l.emit(goir.Op{Code: goir.OpMapGet})
		l.emitUnbox(valT)
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
