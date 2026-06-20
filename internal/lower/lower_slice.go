package lower

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// isValueType reports whether a type needs boxing to be stored as object.
func isValueType(t goir.Type) bool {
	switch t.Kind {
	case goir.KInt64, goir.KInt32, goir.KUint64, goir.KUint32, goir.KFloat64, goir.KFloat32,
		goir.KBool, goir.KString, goir.KStruct, goir.KSlice:
		return true
	default:
		return false
	}
}

// emitBox boxes the value-type value on top of the stack (no-op for object).
func (l *funcLowerer) emitBox(t goir.Type) {
	if isValueType(t) {
		l.emit(goir.Op{Code: goir.OpBox, BoxTy: t})
	}
}

// emitUnbox unboxes the object on top of the stack into a value of type t.
func (l *funcLowerer) emitUnbox(t goir.Type) {
	l.emit(goir.Op{Code: goir.OpUnbox, BoxTy: t})
}

// emitBoxedElem lowers a container element value and boxes it by its own type, so
// concrete values stored into an interface-typed container (e.g. []any, []error)
// are boxed correctly even though the element type is object.
func (l *funcLowerer) emitBoxedElem(v ast.Expr) {
	if isNilIdent(v) {
		l.emit(goir.Op{Code: goir.OpLdNull})
		return
	}
	l.expr(v)
	l.emitBox(l.exprType(v))
}

// emitZeroValue pushes the unboxed zero value of any supported type.
func (l *funcLowerer) emitZeroValue(t goir.Type) {
	switch t.Kind {
	case goir.KStruct:
		tmp := l.addLocal(nil, t)
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		l.emit(goir.Op{Code: goir.OpInitObj, Struct: t.Struct})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	case goir.KSlice:
		// The zero value of a slice is nil (a GoSlice with a null backing array),
		// so `var s []T; s == nil` is true, matching Go.
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "NilSlice",
			Params: []goir.Type{}, Ret: t,
		}})
	case goir.KComplex:
		// Zero complex is 0+0i — a real object (null would break arithmetic).
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: 0})
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: 0})
		l.emit(goir.Op{Code: goir.OpComplexMake})
	case goir.KMap, goir.KPtr, goir.KObject, goir.KObjectArray, goir.KChan, goir.KFunc:
		// An opaque value-type shim (sync.WaitGroup, strings.Builder, …) zeroes to
		// a fresh runtime object; other reference types zero to nil.
		if t.Shim != "" {
			if ext, ok := shimZeroExtern(t.Shim); ok {
				l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
				return
			}
		}
		l.emit(goir.Op{Code: goir.OpLdNull})
	default:
		l.emitZero(t)
	}
}

// emitBoxedZero pushes the boxed zero value (for make / slice / map element zero).
func (l *funcLowerer) emitBoxedZero(t goir.Type) {
	l.emitZeroValue(t)
	l.emitBox(t)
}

// makeCall lowers make([]T, len[, cap]) and make(map[K]V).
func (l *funcLowerer) makeCall(e *ast.CallExpr) goir.Type {
	st := l.exprType(e)
	if st.Kind == goir.KMap {
		l.emit(goir.Op{Code: goir.OpMapMake})
		return st
	}
	if st.Kind == goir.KChan {
		if len(e.Args) >= 2 {
			l.expr(e.Args[1]) // buffer capacity
		} else {
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0}) // unbuffered
		}
		l.emit(goir.Op{Code: goir.OpChanMake})
		return st
	}
	if st.Kind != goir.KSlice {
		l.fail(e.Pos(), "make (only slices and maps are supported)")
		return goir.TVoid
	}
	if len(e.Args) < 2 {
		l.fail(e.Pos(), "make([]T, len) requires a length")
		return goir.TVoid
	}
	lenLocal := l.addLocal(nil, goir.TInt64)
	l.expr(e.Args[1])
	l.emit(goir.Op{Code: goir.OpStLoc, Local: lenLocal})

	l.emit(goir.Op{Code: goir.OpLdLoc, Local: lenLocal}) // len
	if len(e.Args) >= 3 {
		l.expr(e.Args[2]) // cap
	} else {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: lenLocal}) // cap = len
	}
	l.emitBoxedZero(*st.Elem)
	l.emit(goir.Op{Code: goir.OpSliceMake})
	return st
}

// sliceLit lowers a positional slice literal []T{a, b, c}.
func (l *funcLowerer) sliceLit(e *ast.CompositeLit, st goir.Type) goir.Type {
	elem := *st.Elem
	tmp := l.addLocal(nil, st)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(len(e.Elts))})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(len(e.Elts))})
	l.emitBoxedZero(elem)
	l.emit(goir.Op{Code: goir.OpSliceMake})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
	for i, elt := range e.Elts {
		if _, ok := elt.(*ast.KeyValueExpr); ok {
			l.fail(elt.Pos(), "keyed slice literal")
			return goir.TVoid
		}
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(i)})
		l.emitBoxedElem(elt)
		l.emit(goir.Op{Code: goir.OpSliceSet})
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	return st
}

// sliceIndexRead lowers s[i] where s is a slice, leaving the element value.
func (l *funcLowerer) sliceIndexRead(e *ast.IndexExpr, st goir.Type) {
	l.expr(e.X)
	l.expr(e.Index)
	l.emit(goir.Op{Code: goir.OpSliceGet})
	l.emitUnbox(*st.Elem)
}

// sliceIndexWrite lowers s[i] = v where s is a slice.
func (l *funcLowerer) sliceIndexWrite(e *ast.IndexExpr, st goir.Type, rhs ast.Expr) {
	l.expr(e.X)
	l.expr(e.Index)
	l.emitBoxedElem(rhs)
	l.emit(goir.Op{Code: goir.OpSliceSet})
}

// sliceExpr lowers s[lo:hi].
func (l *funcLowerer) sliceExpr(e *ast.SliceExpr) {
	st := l.exprType(e.X)
	if st.Kind != goir.KSlice {
		l.fail(e.Pos(), "slice expression (only slices are supported)")
		return
	}
	tmp := l.addLocal(nil, st)
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})

	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	if e.Low != nil {
		l.expr(e.Low)
	} else {
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	}
	if e.High != nil {
		l.expr(e.High)
	} else {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpSliceLen})
	}
	l.emit(goir.Op{Code: goir.OpSliceSlice})
}

// appendCall lowers append(s, x, y, ...) by chaining single-element appends.
func (l *funcLowerer) appendCall(e *ast.CallExpr) goir.Type {
	if len(e.Args) < 1 {
		l.fail(e.Pos(), "append")
		return goir.TVoid
	}
	st := l.exprType(e.Args[0])
	if st.Kind != goir.KSlice {
		l.fail(e.Pos(), "append to non-slice")
		return goir.TVoid
	}
	if e.Ellipsis.IsValid() {
		l.fail(e.Pos(), "append with ... spread")
		return goir.TVoid
	}
	l.expr(e.Args[0])
	for _, a := range e.Args[1:] {
		l.emitBoxedElem(a)
		l.emit(goir.Op{Code: goir.OpSliceAppend})
	}
	return st
}

// strToSliceConversion lowers []byte(s) / []rune(s).
func (l *funcLowerer) strToSliceConversion(e *ast.CallExpr, target goir.Type) goir.Type {
	sl, ok := l.pkg.TypesInfo.TypeOf(e).Underlying().(*types.Slice)
	if !ok {
		l.fail(e.Pos(), "slice conversion")
		return goir.TVoid
	}
	elem, ok := sl.Elem().Underlying().(*types.Basic)
	if !ok {
		l.fail(e.Pos(), "slice conversion element")
		return goir.TVoid
	}
	l.expr(e.Args[0])
	if elem.Kind() == types.Uint8 {
		l.emit(goir.Op{Code: goir.OpStrToBytes})
	} else {
		l.emit(goir.Op{Code: goir.OpStrToRunes})
	}
	return target
}
