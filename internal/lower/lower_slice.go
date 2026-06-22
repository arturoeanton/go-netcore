package lower

import (
	"go/ast"
	"go/constant"
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
	t := l.exprType(v)
	// Storing an array value into a container, argument, or tuple copies it.
	if t.Array {
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ArrayClone",
			Params: []goir.Type{t}, Ret: t,
		}})
	}
	l.emitBox(t)
}

// emitBoxedElemInto lowers a container element value, boxes it, and — when the
// container's element type is an interface (`[]any`, `map[K]any`, a variadic
// `...any`) — tags it with its named-type identity so the stored value keeps its
// dynamic type for fmt/dispatch/%T. For concrete element types it is exactly
// emitBoxedElem (no wrapping, so e.g. `[]Money` stays comparable/indexable).
func (l *funcLowerer) emitBoxedElemInto(v ast.Expr, elemType goir.Type) {
	// A nil stored into a VALUE-TYPE element slot (the only nil-able value type is a
	// slice: m[k] = nil / s[i] = nil where the element is []T) must box its zero value,
	// not a raw null — the slot is an object[]/Dictionary cell and the value is unboxed
	// back to the GoSlice value type on read, which NREs on a null. The bare nil ident's
	// own type is untyped, so the element type from the container is what tells us this.
	if isNilIdent(v) && isValueType(elemType) {
		l.emitBoxedZero(elemType)
		return
	}
	l.emitBoxedElem(v)
	if elemType.Kind == goir.KObject && !isNilIdent(v) {
		l.maybeWrapNamed(l.pkg.TypesInfo.TypeOf(v))
	}
}

// emitZeroValue pushes the unboxed zero value of any supported type.
func (l *funcLowerer) emitZeroValue(t goir.Type) {
	switch t.Kind {
	case goir.KStruct:
		tmp := l.addLocal(nil, t)
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		l.emit(goir.Op{Code: goir.OpInitObj, Struct: t.Struct})
		l.emitStructOpaqueInits(t.Struct, func() { l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp}) })
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	case goir.KSlice:
		// A fixed-size array [N]T zeroes to a length-N backing with zeroed elements;
		// a slice zeroes to nil (so `var s []T; s == nil` is true, matching Go).
		if t.Array {
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(t.ArrayLen)})
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(t.ArrayLen)})
			l.emitBoxedZero(*t.Elem)
			l.emit(goir.Op{Code: goir.OpSliceMake})
			return
		}
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

// emitArrayZero pushes a fresh slice of n zeroed elements (the zero value of a
// fixed-size [N]T array, which goclr backs with a slice).
func (l *funcLowerer) emitArrayZero(elem goir.Type, n int64) {
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: n})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: n})
	l.emitBoxedZero(elem)
	l.emit(goir.Op{Code: goir.OpSliceMake})
}

// structNeedsOpaqueInit reports whether a struct has (recursively) any opaque
// value-type shim field (sync.Mutex, strings.Builder, …) whose zero value must be a
// fresh runtime object — initobj would otherwise leave it null and crash on use.
func structNeedsOpaqueInit(st *goir.Struct) bool {
	for _, f := range st.Fields {
		if f.Type.Kind == goir.KObject && f.Type.Shim != "" {
			if _, ok := shimZeroExtern(f.Type.Shim); ok {
				return true
			}
		}
		// A fixed-array field ([N]T) zeroes to a length-N backing, not the nil slice
		// that initobj leaves; it needs explicit post-initobj initialization.
		if f.Type.Kind == goir.KSlice && f.Type.Array {
			return true
		}
		if f.Type.Kind == goir.KStruct && structNeedsOpaqueInit(f.Type.Struct) {
			return true
		}
	}
	return false
}

// emitStructOpaqueInits, after a struct's initobj, sets each opaque-shim field to a
// fresh runtime object (and recurses into struct fields). emitAddr pushes the
// managed address of the struct being initialized.
func (l *funcLowerer) emitStructOpaqueInits(st *goir.Struct, emitAddr func()) {
	if !structNeedsOpaqueInit(st) {
		return
	}
	for fi, f := range st.Fields {
		switch {
		case f.Type.Kind == goir.KObject && f.Type.Shim != "":
			if _, ok := shimZeroExtern(f.Type.Shim); ok {
				emitAddr()
				l.emitZeroValue(f.Type) // calls the shim's zero constructor
				l.emit(goir.Op{Code: goir.OpStFld, Struct: st, Field: fi})
			}
		case f.Type.Kind == goir.KSlice && f.Type.Array:
			// Fixed-array field: replace the nil slice left by initobj with a length-N
			// backing of zeroed elements.
			emitAddr()
			l.emitZeroValue(f.Type)
			l.emit(goir.Op{Code: goir.OpStFld, Struct: st, Field: fi})
		case f.Type.Kind == goir.KStruct && structNeedsOpaqueInit(f.Type.Struct):
			i := fi
			l.emitStructOpaqueInits(f.Type.Struct, func() {
				emitAddr()
				l.emit(goir.Op{Code: goir.OpLdFldA, Struct: st, Field: i})
			})
		}
	}
}

// emitBoxedZero pushes the boxed zero value (for make / slice / map element zero).
func (l *funcLowerer) emitBoxedZero(t goir.Type) {
	l.emitZeroValue(t)
	l.emitBox(t)
}

// copyCall lowers copy(dst, src): copies min(len(dst), len(src)) elements and
// returns the count. copy([]byte, string) copies the string's bytes.
func (l *funcLowerer) copyCall(e *ast.CallExpr) goir.Type {
	dstT := l.exprType(e.Args[0])
	srcT := l.exprType(e.Args[1])
	l.expr(e.Args[0])
	l.expr(e.Args[1])
	method, srcParam := "Copy", dstT
	if srcT.Kind == goir.KString {
		method, srcParam = "CopyString", goir.TString
	}
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: method,
		Params: []goir.Type{dstT, srcParam}, Ret: goir.TInt64,
	}})
	return goir.TInt64
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

// sliceLit lowers a slice or array literal: positional ([]T{a, b, c}) or keyed
// ([N]T{0: a, 5: b}). For an array type the backing slice is allocated to the
// declared array length so unspecified elements keep their zero value.
func (l *funcLowerer) sliceLit(e *ast.CompositeLit, st goir.Type) goir.Type {
	elem := *st.Elem

	// Determine the element index of each value (positional indices run from the
	// last key) and the total length to allocate.
	type item struct {
		idx int64
		val ast.Expr
	}
	items := make([]item, 0, len(e.Elts))
	var next, maxIdx int64
	keyed := false
	for _, elt := range e.Elts {
		val := elt
		if kv, ok := elt.(*ast.KeyValueExpr); ok {
			keyed = true
			cv := l.pkg.TypesInfo.Types[kv.Key].Value
			if cv == nil {
				l.fail(kv.Pos(), "slice literal key (must be a constant)")
				return goir.TVoid
			}
			k, ok := constant.Int64Val(constant.ToInt(cv))
			if !ok {
				l.fail(kv.Pos(), "slice literal key value")
				return goir.TVoid
			}
			next = k
			val = kv.Value
		}
		items = append(items, item{idx: next, val: val})
		if next+1 > maxIdx {
			maxIdx = next + 1
		}
		next++
	}

	// Allocation length: the declared array length, else the highest index + 1.
	allocLen := maxIdx
	if arr, ok := l.pkg.TypesInfo.TypeOf(e).Underlying().(*types.Array); ok {
		allocLen = arr.Len()
	}
	_ = keyed

	tmp := l.addLocal(nil, st)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: allocLen})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: allocLen})
	l.emitBoxedZero(elem)
	l.emit(goir.Op{Code: goir.OpSliceMake})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
	for _, it := range items {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: it.idx})
		l.emitBoxedElemInto(it.val, elem)
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
// ptrArrayIndexWrite lowers a[i] = v where a is *[N]T: dereference the pointer to
// the slice-backed array, then store into element i of the shared backing.
func (l *funcLowerer) ptrArrayIndexWrite(e *ast.IndexExpr, arr goir.Type, rhs ast.Expr) {
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpPtrGet})
	l.emitUnbox(arr)
	l.expr(e.Index)
	l.emitBoxedElemInto(rhs, *arr.Elem)
	l.emit(goir.Op{Code: goir.OpSliceSet})
}

func (l *funcLowerer) sliceIndexWrite(e *ast.IndexExpr, st goir.Type, rhs ast.Expr) {
	l.expr(e.X)
	l.expr(e.Index)
	// Box by the element type so a named value stored into an interface-element slice
	// (e.g. code[pc] = jne(target), where code is []instruction and jne is a named
	// int32) is tagged with its typed-box identity — otherwise interface dispatch on
	// that element finds no implementer.
	l.emitBoxedElemInto(rhs, *st.Elem)
	l.emit(goir.Op{Code: goir.OpSliceSet})
}

// sliceExpr lowers s[lo:hi].
func (l *funcLowerer) sliceExpr(e *ast.SliceExpr) {
	st := l.exprType(e.X)
	// s[low:high] on a string yields the byte subrange.
	if st.Kind == goir.KString {
		tmp := l.addLocal(nil, goir.TString)
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
			l.emit(goir.Op{Code: goir.OpStrLen})
		}
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "StrSlice",
			Params: []goir.Type{goir.TString, goir.TInt64, goir.TInt64}, Ret: goir.TString,
		}})
		return
	}
	// p[lo:hi] where p is *[N]T: Go auto-derefs the pointer to the array.
	sliceTy := st
	isPtrArray := st.Kind == goir.KPtr && st.Elem != nil && st.Elem.Kind == goir.KSlice
	if isPtrArray {
		sliceTy = *st.Elem
	}
	if sliceTy.Kind != goir.KSlice {
		l.fail(e.Pos(), "slice expression (only slices are supported)")
		return
	}
	tmp := l.addLocal(nil, sliceTy)
	l.expr(e.X)
	if isPtrArray {
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(sliceTy)
	}
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
		// append(s, other...) — spread the second argument's elements.
		if len(e.Args) != 2 {
			l.fail(e.Pos(), "append spread takes exactly two arguments")
			return goir.TVoid
		}
		l.expr(e.Args[0])
		argT := l.exprType(e.Args[1])
		method := "AppendSlice"
		paramT := st
		if argT.Kind == goir.KString {
			// append([]byte, str...) — append the string's bytes.
			method = "AppendString"
			paramT = goir.TString
		}
		l.expr(e.Args[1])
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: method,
			Params: []goir.Type{st, paramT}, Ret: st,
		}})
		return st
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
