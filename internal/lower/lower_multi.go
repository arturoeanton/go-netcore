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
		idx := i
		l.assignToTarget(s, lhs, rt, func() {
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
			l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(idx)})
			l.emit(goir.Op{Code: goir.OpLdElemRef})
			l.emitUnbox(rt)
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
		t := l.exprType(rhs)
		ttypes[i] = t
		tmp := l.addLocal(nil, t)
		l.expr(rhs)
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
