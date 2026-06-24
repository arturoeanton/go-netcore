package lower

import (
	"go/ast"
	"go/constant"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// expr lowers an expression, leaving its value on the evaluation stack.
func (l *funcLowerer) expr(e ast.Expr) {
	if !l.ok {
		return
	}
	// A shimmed stdlib package symbol (os.Stdout, syscall.SIGINT) resolves to an
	// accessor extern even when it is a typed constant the type checker would otherwise
	// fold to a bare integer — the shim value (e.g. a GoSignal) carries identity the
	// folded constant would lose.
	// A shimmed package variable/const must resolve to its accessor before the
	// globalRef path below, so that a compiled-from-source package (io) referencing its
	// own shim var (EOF, a bare Ident) shares the single shim sentinel rather than its
	// compiled global — otherwise err == io.EOF fails inside io (multiReader, etc.).
	if ext, ok := l.shimVarExtern(e); ok {
		l.emitShimVar(e, ext)
		return
	}
	// Constant folding handles literals, named constants, iota, and constant
	// sub-expressions uniformly.
	if tv := l.pkg.TypesInfo.Types[e]; tv.Value != nil {
		l.emitConst(e, tv.Type, tv.Value)
		return
	}
	// Package-level variable read (ident or pkg.Var selector).
	if gi, ok := l.globalRef(e); ok {
		l.emit(goir.Op{Code: goir.OpLdGlobal, Int: int64(gi)})
		return
	}
	// Shimmed stdlib package variable (os.Stdout, …) -> accessor extern.
	if ext, ok := l.shimVarExtern(e); ok {
		l.emitShimVar(e, ext)
		return
	}
	// A shimmed stdlib function used as a value (unicode.IsSpace, sha256.New) ->
	// a native closure wrapping the shim.
	if l.shimFuncValue(e) {
		return
	}
	switch e := e.(type) {
	case *ast.Ident:
		if e.Name == "nil" {
			// A nil flowing into a slice slot must be the value-type nil slice
			// (default GoSlice), not a null reference.
			if t, ok := l.goType(l.pkg.TypesInfo.TypeOf(e)); ok && t.Kind == goir.KSlice {
				l.emitZeroValue(t)
				return
			}
			l.emit(goir.Op{Code: goir.OpLdNull})
			return
		}
		// A top-level function used as a value (not called) becomes a func value.
		if fn, ok := l.pkg.TypesInfo.ObjectOf(e).(*types.Func); ok && fn.Type().(*types.Signature).Recv() == nil {
			if _, ok := l.funcValue(fn); ok {
				return
			}
		}
		idx, _, ok := l.lookupVar(e)
		if ok {
			l.loadVar(idx)
		}
	case *ast.ParenExpr:
		l.expr(e.X)
	case *ast.StarExpr:
		l.derefRead(e)
	case *ast.BinaryExpr:
		l.binaryExpr(e)
	case *ast.UnaryExpr:
		l.unaryExpr(e)
	case *ast.IndexExpr:
		l.indexExpr(e)
	case *ast.SliceExpr:
		l.sliceExpr(e)
	case *ast.TypeAssertExpr:
		l.typeAssert(e)
	case *ast.SelectorExpr:
		if seln := l.pkg.TypesInfo.Selections[e]; seln != nil {
			if seln.Kind() == types.MethodVal {
				l.methodValue(e, seln)
				return
			}
			// A method expression T.M / (*T).M is a func value whose first parameter
			// is the receiver — exactly funcValue's shape.
			if seln.Kind() == types.MethodExpr {
				if fn, ok := seln.Obj().(*types.Func); ok {
					if _, ok := l.funcValue(fn); ok {
						return
					}
				}
			}
		} else if fn, ok := l.pkg.TypesInfo.ObjectOf(e.Sel).(*types.Func); ok {
			// A qualified reference to a package-level function used as a value, not
			// called (pkg.Func passed as an argument or stored in a map/slice literal,
			// e.g. charset.FromPlain). Emit a func value, like a bare func ident.
			if _, ok := l.funcValue(fn); ok {
				return
			}
		}
		l.fieldRead(e)
	case *ast.CompositeLit:
		l.compositeLit(e)
	case *ast.FuncLit:
		l.closureLit(e)
	case *ast.CallExpr:
		if t := l.callExpr(e); t == goir.TVoid && l.ok {
			l.fail(e.Pos(), "void value used as an expression")
		}
	default:
		l.fail(e.Pos(), "expression")
	}
}

// fieldRead lowers a struct field access p.f (read), leaving the field value on
// the stack. Reading from a value copy is fine; ldfld accepts a struct value.
func (l *funcLowerer) fieldRead(e *ast.SelectorExpr) {
	bt := l.exprType(e.X)
	if bt.Kind == goir.KPtr && bt.Elem.Kind == goir.KStruct {
		l.ptrStructFieldRead(e, bt)
		return
	}
	// Field read on an opaque stdlib shim type (e.g. url.URL.Host) -> getter extern.
	// A *shimType is represented by the same opaque handle as the value (a shim
	// pointer carries no separate cell), so a field read through it uses the same
	// getter — e.g. (*http.Response).Header.
	shim := ""
	if bt.Kind == goir.KObject {
		shim = bt.Shim
	} else if bt.Kind == goir.KPtr && bt.Elem != nil && bt.Elem.Kind == goir.KObject {
		shim = bt.Elem.Shim
	}
	if shim != "" {
		if ext, ok := shimFieldExtern(shim, e.Sel.Name, l.exprType(e)); ok {
			l.expr(e.X)
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
			return
		}
	}
	if bt.Kind != goir.KStruct {
		l.fail(e.Pos(), "selector (only struct field access is supported)")
		return
	}
	fi := bt.Struct.FieldIndex(e.Sel.Name)
	if fi >= 0 {
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: bt.Struct, Field: fi})
		return
	}
	// Promoted field reached through one or more embedded (anonymous) fields:
	// go/types gives the full index path; emit a ldfld chain through it.
	if path, ok := l.promotedFieldPath(e); ok {
		l.expr(e.X)
		l.emitFieldChain(e.Sel.Name, l.exprType(e), e.Pos(), bt, path)
		return
	}
	l.fail(e.Pos(), "unknown field "+e.Sel.Name)
}

// promotedFieldPath returns the chain of (shallow) field indices that go/types
// computed to reach a promoted field selector, if e selects a field.
func (l *funcLowerer) promotedFieldPath(e *ast.SelectorExpr) ([]int, bool) {
	sel := l.pkg.TypesInfo.Selections[e]
	if sel == nil || sel.Kind() != types.FieldVal {
		return nil, false
	}
	idx := sel.Index()
	if len(idx) < 2 {
		return nil, false
	}
	return idx, true
}

// emitFieldChain emits a ldfld chain that walks `path` (shallow field indices)
// starting from a struct value already on the stack, dereferencing any embedded
// pointer field along the way. The final field's value is left on the stack, and
// its type is returned.
func (l *funcLowerer) emitFieldChain(fieldName string, resultType goir.Type, pos token.Pos, structType goir.Type, path []int) goir.Type {
	cur := structType
	for _, idx := range path {
		if cur.Kind == goir.KPtr {
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emitUnbox(*cur.Elem)
			cur = *cur.Elem
		}
		// The chain reached an embedded opaque shim (e.g. ecdsa.PrivateKey embeds
		// ecdsa.PublicKey). The one handle represents the whole embedded chain, so any
		// further navigation into it is a no-op: for a leaf field read, call the shim's
		// registered getter; for navigation (no field name), stop with the handle on the
		// stack — the promoted method/field then resolves on it.
		if cur.Kind == goir.KObject && cur.Shim != "" {
			if fieldName == "" {
				return cur
			}
			if ext, ok := shimFieldExtern(cur.Shim, fieldName, resultType); ok {
				l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
				return resultType
			}
		}
		if cur.Kind != goir.KStruct || idx >= len(cur.Struct.Fields) {
			l.fail(pos, "promoted field path")
			return goir.TVoid
		}
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: cur.Struct, Field: idx})
		cur = cur.Struct.Fields[idx].Type
	}
	return cur
}

// compositeLit lowers a struct composite literal Point{...} (keyed or positional)
// into a temp local: zero-initialize, set provided fields, then push the value.
// keyedElt returns the value of a keyed composite-literal field by name, or nil.
func keyedElt(e *ast.CompositeLit, name string) ast.Expr {
	for _, elt := range e.Elts {
		if kv, ok := elt.(*ast.KeyValueExpr); ok {
			if id, ok := kv.Key.(*ast.Ident); ok && id.Name == name {
				return kv.Value
			}
		}
	}
	return nil
}

func (l *funcLowerer) compositeLit(e *ast.CompositeLit) goir.Type {
	t := l.exprType(e)
	switch t.Kind {
	case goir.KStruct:
		// handled below
	case goir.KSlice:
		return l.sliceLit(e, t)
	case goir.KMap:
		return l.mapLit(e, t)
	case goir.KObject:
		// Composite literal of an opaque value-type shim (e.g. bytes.Buffer{}) —
		// produce its zero value object (field initializers are not supported,
		// except sync.Pool{New: fn} whose factory must be captured).
		if t.Shim != "" {
			if t.Shim == "sync.Pool" {
				if fn := keyedElt(e, "New"); fn != nil {
					l.expr(fn)
					l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
						Assembly: shimAssembly, Namespace: shimAssembly, Type: "Sync", Method: "NewPoolWith",
						Params: []goir.Type{goir.TFunc}, Ret: t,
					}})
					return t
				}
			}
			l.emitZeroValue(t)
			l.applyShimFields(e, t, l.pkg.TypesInfo.TypeOf(e))
			return t
		}
		l.fail(e.Pos(), "composite literal of "+t.Shim)
		return goir.TVoid
	case goir.KPtr:
		// An elided &-pointer struct literal, e.g. []*T{{...}} or map[K]*T{k: {...}}:
		// build the pointee struct and take its address.
		if t.Elem != nil && t.Elem.Kind == goir.KStruct {
			l.structLit(e, *t.Elem)
			l.emitBox(*t.Elem)
			l.ptrNew(*t.Elem)
			return t
		}
		// &shim{...}: an opaque shim and its pointer are the same runtime object, so
		// build the shim value (zero + field setters) directly.
		if t.Elem != nil && t.Elem.Kind == goir.KObject && t.Elem.Shim != "" {
			l.emitZeroValue(*t.Elem)
			l.applyShimFields(e, *t.Elem, l.pkg.TypesInfo.TypeOf(e))
			return *t.Elem
		}
		l.fail(e.Pos(), "composite literal (pointer)")
		return goir.TVoid
	default:
		l.fail(e.Pos(), "composite literal (only struct, slice and map literals are supported)")
		return goir.TVoid
	}
	l.structLit(e, t)
	return t
}

// applyShimFields applies the keyed field initializers of an opaque-shim composite
// literal (e.g. http.Cookie{Name: "x", MaxAge: 60}) by calling each field's registered
// setter on the zero value left on the stack by emitZeroValue. litType is the literal's
// go/types type (a shim value or pointer), used to coerce each value to the field type.
func (l *funcLowerer) applyShimFields(e *ast.CompositeLit, t goir.Type, litType types.Type) {
	if len(e.Elts) == 0 {
		return
	}
	// Best-effort: a shim with no field setters keeps its zero value (the long-standing
	// behavior — e.g. syscall.Flock_t{...}, a no-op lock whose fields are unobserved).
	setters, ok := shimFieldSetRegistry[t.Shim]
	if !ok {
		return
	}
	var st *types.Struct
	if named := namedOf(litType); named != nil {
		st, _ = named.Underlying().(*types.Struct)
	}
	tmp := l.addLocal(nil, t)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
	for _, elt := range e.Elts {
		kv, ok := elt.(*ast.KeyValueExpr)
		if !ok {
			continue // positional shim field: keep the zero value for it
		}
		id, ok := kv.Key.(*ast.Ident)
		if !ok {
			continue
		}
		sf, ok := setters[id.Name]
		if !ok {
			continue // no setter for this field: keep its zero value
		}
		ft := l.exprType(kv.Value)
		if st != nil {
			for i := 0; i < st.NumFields(); i++ {
				if st.Field(i).Name() == id.Name {
					if it, ok := l.goType(st.Field(i).Type()); ok {
						ft = it
					}
					break
				}
			}
		}
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.exprCoerced(kv.Value, ft)
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod,
			Params: []goir.Type{goir.TObject, ft}, Ret: goir.TVoid,
		}})
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
}

// structLit builds a struct composite literal of type st (keyed or positional),
// leaving the struct value on the stack.
func (l *funcLowerer) structLit(e *ast.CompositeLit, st goir.Type) {
	s := st.Struct
	tmp := l.addLocal(nil, st)
	l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
	l.emit(goir.Op{Code: goir.OpInitObj, Struct: s})
	l.emitStructOpaqueInits(s, func() { l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp}) })

	for i, elt := range e.Elts {
		var fi int
		var val ast.Expr
		if kv, ok := elt.(*ast.KeyValueExpr); ok {
			key, ok := kv.Key.(*ast.Ident)
			if !ok {
				l.fail(kv.Pos(), "composite literal key")
				return
			}
			fi = s.FieldIndex(key.Name)
			val = kv.Value
		} else {
			fi = i // positional
			val = elt
		}
		if fi < 0 || fi >= len(s.Fields) {
			l.fail(elt.Pos(), "composite literal field")
			return
		}
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		l.exprCoerced(val, s.Fields[fi].Type)
		l.emit(goir.Op{Code: goir.OpStFld, Struct: s, Field: fi})
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
}

// indexExpr lowers s[i] for strings (yielding the byte) and slices.
func (l *funcLowerer) indexExpr(e *ast.IndexExpr) {
	xt := l.exprType(e.X)
	switch xt.Kind {
	case goir.KString:
		l.expr(e.X)
		l.expr(e.Index)
		l.emit(goir.Op{Code: goir.OpStrIndex})
	case goir.KSlice:
		l.sliceIndexRead(e, xt)
	case goir.KMap:
		l.mapIndexRead(e, xt)
	case goir.KPtr:
		// (*p)[i] where p is a pointer to an array: Go auto-dereferences.
		if xt.Elem != nil && xt.Elem.Kind == goir.KSlice {
			st := *xt.Elem
			l.expr(e.X)
			l.emit(goir.Op{Code: goir.OpPtrGet})
			l.emitUnbox(st)
			l.expr(e.Index)
			l.emit(goir.Op{Code: goir.OpSliceGet})
			l.emitUnbox(*st.Elem)
			return
		}
		l.fail(e.Pos(), "indexing (only strings, slices and maps are supported)")
	default:
		l.fail(e.Pos(), "indexing (only strings, slices and maps are supported)")
	}
}

func (l *funcLowerer) emitConst(e ast.Expr, t types.Type, v constant.Value) {
	gt, ok := l.goType(t)
	if !ok {
		l.fail(e.Pos(), "constant type")
		return
	}
	switch gt.Kind {
	case goir.KInt64:
		iv, _ := constant.Int64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: iv})
	case goir.KInt32:
		iv, _ := constant.Int64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: iv})
	case goir.KUint64:
		uv, _ := constant.Uint64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(uv)})
	case goir.KUint32:
		uv, _ := constant.Uint64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(uv)})
	case goir.KFloat64:
		fv, _ := constant.Float64Val(constant.ToFloat(v))
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: fv})
	case goir.KFloat32:
		fv, _ := constant.Float64Val(constant.ToFloat(v))
		l.emit(goir.Op{Code: goir.OpLdcR4, Float: fv})
	case goir.KComplex:
		cv := constant.ToComplex(v)
		re, _ := constant.Float64Val(constant.Real(cv))
		im, _ := constant.Float64Val(constant.Imag(cv))
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: re})
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: im})
		l.emit(goir.Op{Code: goir.OpComplexMake})
	case goir.KBool:
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: b2i(constant.BoolVal(v))})
	case goir.KString:
		l.emit(goir.Op{Code: goir.OpStrConst, Str: constant.StringVal(v)})
	default:
		l.fail(e.Pos(), "constant")
	}
}

func (l *funcLowerer) binaryExpr(e *ast.BinaryExpr) {
	switch e.Op {
	case token.LAND:
		// a && b : short-circuit.
		lfalse, lend := l.label(), l.label()
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpBrFalse, Label: lfalse})
		l.expr(e.Y)
		l.emit(goir.Op{Code: goir.OpBr, Label: lend})
		l.mark(lfalse)
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		l.mark(lend)
		return
	case token.LOR:
		// a || b : short-circuit.
		ltrue, lend := l.label(), l.label()
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: ltrue})
		l.expr(e.Y)
		l.emit(goir.Op{Code: goir.OpBr, Label: lend})
		l.mark(ltrue)
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 1})
		l.mark(lend)
		return
	case token.EQL, token.NEQ, token.LSS, token.LEQ, token.GTR, token.GEQ:
		opType := l.cmpOperandType(e.X, e.Y)
		if opType.Kind == goir.KSlice {
			// A slice value can only be compared against nil (test its backing
			// array). Two non-nil slice-typed operands are a fixed-array comparison
			// (the type checker rejects slice==slice), which is element-wise by value.
			if isNilIdent(e.X) || isNilIdent(e.Y) {
				operand := e.X
				if isNilIdent(e.X) {
					operand = e.Y
				}
				l.expr(operand)
				l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
					Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "SliceIsNil",
					Params: []goir.Type{opType}, Ret: goir.TBool,
				}})
				if e.Op == token.NEQ {
					l.emit(goir.Op{Code: goir.OpNot})
				}
				return
			}
			l.emitValueEqual(e, opType)
			return
		}
		// Structs and arrays compare by value (== is element/field-wise in Go), not
		// by the reference identity of their boxed runtime objects.
		if opType.Kind == goir.KStruct {
			l.emitValueEqual(e, opType)
			return
		}
		l.expr(e.X)
		l.expr(e.Y)
		l.compare(e.Op, opType)
		return
	}

	// Arithmetic / bitwise / string-concat / complex (operand-type directed).
	if _, ok := arithOp(e.Op); !ok && e.Op != token.AND_NOT {
		l.fail(e.Pos(), "operator "+e.Op.String())
		return
	}
	opType := l.exprType(e.X)
	l.expr(e.X)
	l.expr(e.Y)
	l.emitArith(e.Op, opType)
}

// emitShimVar calls a shimmed-package-variable accessor (Ret is always object) and unboxes
// the result to the variable's actual type when that type is a value type (a slice like
// os.Args, a struct) — reference-typed vars (os.Stdout) are left as the object handle.
func (l *funcLowerer) emitShimVar(e ast.Expr, ext *goir.Extern) {
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	if vt := l.exprType(e); isValueType(vt) {
		l.emitUnbox(vt)
	}
}

func isUnsigned(t goir.Type) bool {
	return t.Kind == goir.KUint64 || t.Kind == goir.KUint32
}

// emitValueEqual lowers a struct or array == / != as a by-value comparison:
// both operands are boxed and handed to the runtime, which compares fields and
// elements recursively.
func (l *funcLowerer) emitValueEqual(e *ast.BinaryExpr, opType goir.Type) {
	l.expr(e.X)
	l.emitBox(opType)
	l.expr(e.Y)
	l.emitBox(opType)
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ValueEqual",
		Params: []goir.Type{goir.TObject, goir.TObject}, Ret: goir.TBool,
	}})
	if e.Op == token.NEQ {
		l.emit(goir.Op{Code: goir.OpNot})
	}
}

// cmpOperandType returns the operand type of a comparison, ignoring a nil operand.
func (l *funcLowerer) cmpOperandType(x, y ast.Expr) goir.Type {
	if !isNilIdent(x) {
		return l.exprType(x)
	}
	return l.exprType(y)
}

// compare emits a comparison for operands already on the stack.
func (l *funcLowerer) compare(op token.Token, operandType goir.Type) {
	if operandType == goir.TString {
		l.compareString(op)
		return
	}
	if operandType.Kind == goir.KMap {
		// A map is only comparable to nil. Both the bare-null and GoMap{Data:null}
		// representations of a nil map must compare equal to nil, so route through a
		// helper rather than a reference compare.
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "MapNilEq",
			Params: []goir.Type{goir.TObject, goir.TObject}, Ret: goir.TBool,
		}})
		if op == token.NEQ {
			l.emit(goir.Op{Code: goir.OpNot})
		}
		return
	}
	if operandType.Shim != "" {
		// An opaque stdlib value type (e.g. netip.Addr) is comparable by value in Go,
		// but its runtime handle is a reference object; compare field-wise.
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ValueEqual",
			Params: []goir.Type{goir.TObject, goir.TObject}, Ret: goir.TBool,
		}})
		if op == token.NEQ {
			l.emit(goir.Op{Code: goir.OpNot})
		}
		return
	}
	if operandType.Kind == goir.KPtr || operandType.Kind == goir.KObject {
		// Reference equality (pointers, interface-vs-nil).
		l.emit(goir.Op{Code: goir.OpCeq})
		if op == token.NEQ {
			l.emit(goir.Op{Code: goir.OpNot})
		}
		return
	}
	if operandType.Kind == goir.KComplex {
		// Complex has no ordering; only == / != (structural equality).
		l.emit(goir.Op{Code: goir.OpComplexEq})
		if op == token.NEQ {
			l.emit(goir.Op{Code: goir.OpNot})
		}
		return
	}
	lt, gt := goir.OpClt, goir.OpCgt
	if isUnsigned(operandType) {
		lt, gt = goir.OpCltUn, goir.OpCgtUn
	}
	switch op {
	case token.EQL:
		l.emit(goir.Op{Code: goir.OpCeq})
	case token.NEQ:
		l.emit(goir.Op{Code: goir.OpCeq})
		l.emit(goir.Op{Code: goir.OpNot})
	case token.LSS:
		l.emit(goir.Op{Code: lt})
	case token.GTR:
		l.emit(goir.Op{Code: gt})
	case token.LEQ:
		l.emit(goir.Op{Code: gt})
		l.emit(goir.Op{Code: goir.OpNot})
	case token.GEQ:
		l.emit(goir.Op{Code: lt})
		l.emit(goir.Op{Code: goir.OpNot})
	}
}

// compareString emits a comparison for two GoStrings already on the stack.
// Equality uses GoStrings.Equal; ordering uses GoStrings.Compare against 0.
func (l *funcLowerer) compareString(op token.Token) {
	if op == token.EQL || op == token.NEQ {
		l.emit(goir.Op{Code: goir.OpStrEqual})
		if op == token.NEQ {
			l.emit(goir.Op{Code: goir.OpNot})
		}
		return
	}
	l.emit(goir.Op{Code: goir.OpStrCompare}) // -> int (<0, 0, >0)
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	switch op {
	case token.LSS:
		l.emit(goir.Op{Code: goir.OpClt})
	case token.GTR:
		l.emit(goir.Op{Code: goir.OpCgt})
	case token.LEQ:
		l.emit(goir.Op{Code: goir.OpCgt})
		l.emit(goir.Op{Code: goir.OpNot})
	case token.GEQ:
		l.emit(goir.Op{Code: goir.OpClt})
		l.emit(goir.Op{Code: goir.OpNot})
	}
}

func (l *funcLowerer) unaryExpr(e *ast.UnaryExpr) {
	switch e.Op {
	case token.SUB:
		if l.exprType(e.X).Kind == goir.KComplex {
			l.expr(e.X)
			l.emit(goir.Op{Code: goir.OpComplexNeg})
			return
		}
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpNeg})
	case token.NOT:
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpNot})
	case token.ADD:
		l.expr(e.X)
	case token.XOR: // bitwise complement: ^x == x ^ -1
		t := l.exprType(e.X)
		l.expr(e.X)
		l.emitInt(-1, t)
		l.emit(goir.Op{Code: goir.OpXor})
	case token.AND: // &operand
		l.addrOf(e)
	case token.ARROW: // <-ch : receive a value
		ct := l.exprType(e.X)
		if ct.Kind != goir.KChan {
			l.fail(e.Pos(), "receive from non-channel")
			return
		}
		l.expr(e.X)
		l.emit(goir.Op{Code: goir.OpChanRecv})
		l.emitRecvUnbox(*ct.Elem)
	default:
		l.fail(e.Pos(), "unary "+e.Op.String())
	}
}

// namedFuncCall lowers a call to a free function (same-package ident or
// cross-package pkg.Func), resolving the callee through the global byFunc table,
// or monomorphizing a generic function.
func (l *funcLowerer) namedFuncCall(e *ast.CallExpr, ident *ast.Ident, fn *types.Func) goir.Type {
	// json.Unmarshal needs the target's static type, encoded as a descriptor.
	if fn.Pkg() != nil && fn.Pkg().Path() == "encoding/json" && fn.Name() == "Unmarshal" {
		return l.jsonUnmarshalCall(e)
	}
	// errors.As needs the target's concrete type to match against the chain.
	if fn.Pkg() != nil && fn.Pkg().Path() == "errors" && fn.Name() == "As" {
		return l.errorsAsCall(e)
	}
	// reflect.TypeOf/ValueOf carry the argument's static-type descriptor id, so
	// reflect has precise type info without inspecting a sample value.
	if fn.Pkg() != nil && fn.Pkg().Path() == "reflect" && (fn.Name() == "TypeOf" || fn.Name() == "ValueOf") {
		return l.reflectOfCall(e, fn.Name())
	}
	if fn.Pkg() != nil && fn.Pkg().Path() == "reflect" && fn.Name() == "TypeFor" {
		return l.reflectTypeForCall(e)
	}
	// Shimmed stdlib function -> external (GoCLR.Stdlib) call.
	if ext, ok := l.shimExtern(fn); ok {
		variadic := false
		if sig, ok := fn.Type().(*types.Signature); ok {
			variadic = sig.Variadic()
		}
		return l.shimCall(e, ext, variadic)
	}
	callee, ok := l.byFunc[fn]
	if !ok {
		callee, ok = l.genericCallee(ident)
		if !ok {
			l.fail(e.Pos(), "call to "+ident.Name)
			return goir.TVoid
		}
	}
	variadic := false
	if sig, ok := fn.Type().(*types.Signature); ok {
		variadic = sig.Variadic()
	}
	l.emitCallArgs(e.Args, callee.Params, variadic, e.Ellipsis.IsValid())
	l.emit(goir.Op{Code: goir.OpCallMethod, Callee: callee})
	return callee.Ret
}

// explicitGenericFun reports whether fun is an explicit generic instantiation of a
// free function (Fn[T] or pkg.Fn[T]), returning the function identifier and object.
// Index/IndexListExpr that are array/slice indexing or generic type conversions are
// not matched (those resolve elsewhere).
func (l *funcLowerer) explicitGenericFun(fun ast.Expr) (*ast.Ident, *types.Func, bool) {
	var x ast.Expr
	switch ix := fun.(type) {
	case *ast.IndexExpr:
		x = ix.X
	case *ast.IndexListExpr:
		x = ix.X
	default:
		return nil, nil, false
	}
	var id *ast.Ident
	switch xx := x.(type) {
	case *ast.Ident:
		id = xx
	case *ast.SelectorExpr:
		id = xx.Sel
	default:
		return nil, nil, false
	}
	fn, ok := l.pkg.TypesInfo.Uses[id].(*types.Func)
	if !ok || fn.Pkg() == nil {
		return nil, nil, false
	}
	if sig, ok := fn.Type().(*types.Signature); !ok || sig.Recv() != nil {
		return nil, nil, false // methods dispatch through methodCall
	}
	return id, fn, true
}

// callExpr lowers a call and returns its result type (TVoid for none).
// unsafeBuiltin reports whether sel names an `unsafe.<X>` builtin (unsafe.String,
// unsafe.SliceData, …), returning its name.
func (l *funcLowerer) unsafeBuiltin(sel *ast.SelectorExpr) (string, bool) {
	if _, ok := l.pkg.TypesInfo.Uses[sel.Sel].(*types.Builtin); !ok {
		return "", false
	}
	if id, ok := sel.X.(*ast.Ident); ok {
		if pkgName, ok := l.pkg.TypesInfo.Uses[id].(*types.PkgName); ok && pkgName.Imported().Path() == "unsafe" {
			return sel.Sel.Name, true
		}
	}
	return "", false
}

// unsafeDataArg matches `unsafe.<builtin>(X)` (X = a []byte or string) and returns X.
func (l *funcLowerer) unsafeDataArg(arg ast.Expr, builtin string) (ast.Expr, bool) {
	call, ok := unparen(arg).(*ast.CallExpr)
	if !ok {
		return nil, false
	}
	sel, ok := call.Fun.(*ast.SelectorExpr)
	if !ok {
		return nil, false
	}
	if name, ok := l.unsafeBuiltin(sel); ok && name == builtin && len(call.Args) == 1 {
		return call.Args[0], true
	}
	return nil, false
}

// unsafeCall lowers the safe `string ↔ []byte` reinterpret idioms expressed with Go 1.20+
// unsafe builtins, as a copying conversion (goclr has no raw memory; see
// docs/DESIGN-unsafe-pointer.md). Only the composed forms are representable:
//
//	unsafe.String(unsafe.SliceData(b), n)  ->  string(b[:n])
//	unsafe.Slice(unsafe.StringData(s), n)  ->  []byte(s[:n])
func (l *funcLowerer) unsafeCall(e *ast.CallExpr, name string) goir.Type {
	switch name {
	case "String":
		if x, ok := l.unsafeDataArg(e.Args[0], "SliceData"); ok {
			ret := l.exprType(e) // string
			l.expr(x)            // the []byte
			l.exprCoerced(e.Args[1], goir.TInt64)
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: "Unsafe", Method: "String",
				Params: []goir.Type{l.exprType(x), goir.TInt64}, Ret: ret,
			}})
			return ret
		}
	case "Slice":
		if x, ok := l.unsafeDataArg(e.Args[0], "StringData"); ok {
			ret := l.exprType(e) // []byte
			l.expr(x)            // the string
			l.exprCoerced(e.Args[1], goir.TInt64)
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: "Unsafe", Method: "Slice",
				Params: []goir.Type{goir.TString, goir.TInt64}, Ret: ret,
			}})
			return ret
		}
	}
	l.fail(e.Pos(), "unsafe."+name+" (only the unsafe.String/Slice string↔[]byte reinterpret idiom is supported)")
	return goir.TVoid
}

// headerViewConversion recognizes `(*reflect.SliceHeader)(unsafe.Pointer(&x))` /
// `(*reflect.StringHeader)(...)` and lowers it to a read-only header view over x (a
// GoHeaderView, see Reflect.SH_*), so .Data/.Len/.Cap reads — the only safe use, e.g.
// computing a sub-slice's byte offset within its parent — work without raw memory.
func (l *funcLowerer) headerViewConversion(e *ast.CallExpr) (goir.Type, bool) {
	pt, ok := l.pkg.TypesInfo.TypeOf(e).Underlying().(*types.Pointer)
	if !ok {
		return goir.TVoid, false
	}
	named, ok := types.Unalias(pt.Elem()).(*types.Named)
	if !ok || named.Obj().Pkg() == nil || named.Obj().Pkg().Path() != "reflect" {
		return goir.TVoid, false
	}
	var method string
	switch named.Obj().Name() {
	case "SliceHeader":
		method = "SliceHeaderOf"
	case "StringHeader":
		method = "StringHeaderOf"
	default:
		return goir.TVoid, false
	}
	inner, ok := unparen(e.Args[0]).(*ast.CallExpr)
	if !ok || len(inner.Args) != 1 {
		return goir.TVoid, false
	}
	if b, ok := l.pkg.TypesInfo.TypeOf(inner).Underlying().(*types.Basic); !ok || b.Kind() != types.UnsafePointer {
		return goir.TVoid, false
	}
	addr, ok := unparen(inner.Args[0]).(*ast.UnaryExpr)
	if !ok || addr.Op != token.AND {
		return goir.TVoid, false
	}
	ret := goir.Type{Kind: goir.KObject, Shim: "reflect." + named.Obj().Name()}
	l.expr(addr.X) // the slice (SliceHeader) or string (StringHeader)
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Reflect", Method: method,
		Params: []goir.Type{l.exprType(addr.X)}, Ret: ret,
	}})
	return ret, true
}

func (l *funcLowerer) callExpr(e *ast.CallExpr) goir.Type {
	// Type conversions: T(x), including []byte(s) / []rune(s) where the call
	// target is a type expression rather than a plain identifier.
	if tv, ok := l.pkg.TypesInfo.Types[e.Fun]; ok && tv.IsType() {
		return l.conversion(e)
	}
	// Method calls: recv.Method(args).
	if sel, ok := e.Fun.(*ast.SelectorExpr); ok {
		// unsafe.String/Slice — the safe string<->[]byte reinterpret idioms.
		if name, ok := l.unsafeBuiltin(sel); ok {
			return l.unsafeCall(e, name)
		}
		if seln := l.pkg.TypesInfo.Selections[sel]; seln != nil && seln.Kind() == types.MethodVal {
			return l.methodCall(e, sel, seln)
		}
		// Package-qualified free function call: pkg.Func(args).
		if fn, ok := l.pkg.TypesInfo.Uses[sel.Sel].(*types.Func); ok {
			if sig, ok := fn.Type().(*types.Signature); ok && sig.Recv() == nil {
				return l.namedFuncCall(e, sel.Sel, fn)
			}
		}
	}
	// Explicit generic instantiation: Fn[T](args) / pkg.Fn[T](args). The callee is
	// an Index/IndexListExpr whose X names a generic free function; route it through
	// the normal generic-call path (TypesInfo.Instances records the type args).
	if id, fn, ok := l.explicitGenericFun(e.Fun); ok {
		return l.namedFuncCall(e, id, fn)
	}
	// Calling a function value (closure / func variable).
	if l.isFuncValue(e.Fun) {
		return l.funcValueCall(e)
	}
	fun, ok := e.Fun.(*ast.Ident)
	if !ok {
		l.fail(e.Fun.Pos(), "call target")
		return goir.TVoid
	}
	obj := l.pkg.TypesInfo.Uses[fun]

	switch o := obj.(type) {
	case *types.Builtin:
		switch o.Name() {
		case "println":
			return l.printCall(e, true)
		case "print":
			return l.printCall(e, false)
		case "len":
			return l.lenCall(e)
		case "cap":
			return l.capCall(e)
		case "make":
			return l.makeCall(e)
		case "append":
			return l.appendCall(e)
		case "copy":
			return l.copyCall(e)
		case "delete":
			return l.deleteCall(e)
		case "new":
			return l.newCall(e)
		case "panic":
			l.exprCoerced(e.Args[0], goir.TObject) // box the panic value
			l.emit(goir.Op{Code: goir.OpCallPanic})
			return goir.TVoid
		case "recover":
			l.emit(goir.Op{Code: goir.OpCallRecover})
			return goir.TObject
		case "close":
			l.expr(e.Args[0])
			l.emit(goir.Op{Code: goir.OpChanClose})
			return goir.TVoid
		case "complex":
			l.exprCoerced(e.Args[0], goir.TFloat64)
			l.exprCoerced(e.Args[1], goir.TFloat64)
			l.emit(goir.Op{Code: goir.OpComplexMake})
			return goir.TComplex
		case "real":
			l.expr(e.Args[0])
			l.emit(goir.Op{Code: goir.OpComplexReal})
			return goir.TFloat64
		case "imag":
			l.expr(e.Args[0])
			l.emit(goir.Op{Code: goir.OpComplexImag})
			return goir.TFloat64
		case "clear":
			return l.clearCall(e)
		case "max":
			return l.maxMinCall(e, true)
		case "min":
			return l.maxMinCall(e, false)
		default:
			l.fail(e.Pos(), "builtin "+o.Name())
			return goir.TVoid
		}
	case *types.TypeName:
		// Type conversion, e.g. int64(x), int32(x), rune(x).
		return l.conversion(e)
	case *types.Func:
		return l.namedFuncCall(e, fun, o)
	default:
		l.fail(e.Pos(), "call to "+fun.Name)
		return goir.TVoid
	}
}

func (l *funcLowerer) conversion(e *ast.CallExpr) goir.Type {
	if len(e.Args) != 1 {
		l.fail(e.Pos(), "conversion")
		return goir.TVoid
	}
	// (*reflect.SliceHeader)(unsafe.Pointer(&x)) — a read-only header view over x.
	if t, ok := l.headerViewConversion(e); ok {
		return t
	}
	// A conversion to unsafe.Pointer that reached here is an UNRECOGNIZED reinterpret
	// (the safe string↔[]byte idioms and reflect.*Header reads short-circuit earlier, in
	// derefRead / the header-view recognizer). goclr has no raw memory, so a real
	// pointer reinterpret/bit-cast can't be represented — reject it rather than silently
	// miscompile. (unsafe.Pointer is allow-listed in the analysis pass; this is its gate.)
	if b, ok := l.pkg.TypesInfo.TypeOf(e).Underlying().(*types.Basic); ok && b.Kind() == types.UnsafePointer {
		l.fail(e.Pos(), "unsafe.Pointer reinterpret (only the string↔[]byte and reflect.*Header idioms are supported)")
		return goir.TVoid
	}
	target, ok := l.goType(l.pkg.TypesInfo.TypeOf(e))
	if !ok {
		l.fail(e.Pos(), "conversion target type")
		return goir.TVoid
	}
	// T(nil) — e.g. []rune(nil), map[K]V(nil) — is the target's zero value. Handle
	// before exprType(arg), which has no type for the untyped nil.
	if isNilIdent(e.Args[0]) {
		l.emitZeroValue(target)
		return target
	}
	// []byte(s) / []rune(s).
	if target.Kind == goir.KSlice && l.exprType(e.Args[0]) == goir.TString {
		return l.strToSliceConversion(e, target)
	}
	// string(x): from a rune/int code point, []byte, or []rune.
	if target.Kind == goir.KString {
		argT := l.pkg.TypesInfo.TypeOf(e.Args[0])
		if sl, ok := argT.Underlying().(*types.Slice); ok {
			l.expr(e.Args[0])
			if b, ok := sl.Elem().Underlying().(*types.Basic); ok && b.Kind() == types.Uint8 {
				l.emit(goir.Op{Code: goir.OpStrFromBytes})
			} else {
				l.emit(goir.Op{Code: goir.OpStrFromRunes})
			}
			return target
		}
		if b, ok := argT.Underlying().(*types.Basic); ok && b.Info()&types.IsInteger != 0 {
			l.expr(e.Args[0])
			if k := l.exprType(e.Args[0]).Kind; k == goir.KInt32 || k == goir.KUint32 {
				l.emit(goir.Op{Code: goir.OpConvI8})
			}
			l.emit(goir.Op{Code: goir.OpStrFromRune})
			return target
		}
		l.expr(e.Args[0]) // string(string) identity
		return target
	}
	// A conversion between two named struct types with the same underlying layout
	// (type Tag compact.Tag) is a no-op in Go, but goclr gives them distinct CLR
	// types, so the bytes must be reinterpreted field-by-field. Covers both the value
	// form B(a) and the pointer form (*B)(p).
	srcT := l.exprType(e.Args[0])
	if target.Kind == goir.KStruct && srcT.Kind == goir.KStruct && srcT.Struct != target.Struct {
		l.expr(e.Args[0])
		l.emitStructReinterpret(srcT, target)
		return target
	}
	if target.Kind == goir.KPtr && target.Elem.Kind == goir.KStruct &&
		srcT.Kind == goir.KPtr && srcT.Elem.Kind == goir.KStruct && srcT.Elem.Struct != target.Elem.Struct {
		// (*B)(p): read the pointee, reinterpret it as B, and box into a fresh cell.
		// Aliasing is not preserved (a new cell), which suffices for the read-only
		// punning these conversions perform (e.g. (*compact.Tag)(t).IsCompact()).
		l.expr(e.Args[0])
		l.emit(goir.Op{Code: goir.OpPtrGet})
		l.emitUnbox(*srcT.Elem)
		l.emitStructReinterpret(*srcT.Elem, *target.Elem)
		l.emitBox(*target.Elem)
		l.ptrNew(*target.Elem)
		return target
	}

	l.expr(e.Args[0])
	switch target.Kind {
	case goir.KInt64:
		l.emit(goir.Op{Code: goir.OpConvI8})
	case goir.KInt32:
		l.emit(goir.Op{Code: goir.OpConvI4})
	case goir.KUint64:
		l.emit(goir.Op{Code: goir.OpConvU8})
	case goir.KUint32:
		l.emit(goir.Op{Code: goir.OpConvU4})
	case goir.KFloat64:
		l.emit(goir.Op{Code: goir.OpConvR8})
	case goir.KFloat32:
		l.emit(goir.Op{Code: goir.OpConvR4})
	case goir.KString:
		l.fail(e.Pos(), "string conversion")
	}
	// Converting to a sub-word integer truncates to its width (uint8(300) == 44).
	l.truncateInt(target)
	return target
}

// emitStructReinterpret converts a struct value of srcT (on the stack) to an
// identical-layout struct value of dstT, copying each field across the two distinct
// CLR types. The field lists match (the types share an underlying *types.Struct).
func (l *funcLowerer) emitStructReinterpret(srcT, dstT goir.Type) {
	sTmp := l.addLocal(nil, srcT)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: sTmp})
	dTmp := l.addLocal(nil, dstT)
	l.emit(goir.Op{Code: goir.OpLdLocA, Local: dTmp})
	l.emit(goir.Op{Code: goir.OpInitObj, Struct: dstT.Struct})
	n := len(dstT.Struct.Fields)
	if len(srcT.Struct.Fields) < n {
		n = len(srcT.Struct.Fields)
	}
	for i := 0; i < n; i++ {
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: dTmp})
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: sTmp})
		l.emit(goir.Op{Code: goir.OpLdFld, Struct: srcT.Struct, Field: i})
		l.emit(goir.Op{Code: goir.OpStFld, Struct: dstT.Struct, Field: i})
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: dTmp})
}

// lenCall lowers len(s) for strings (byte length) and slices.
func (l *funcLowerer) lenCall(e *ast.CallExpr) goir.Type {
	if len(e.Args) != 1 {
		l.fail(e.Pos(), "len")
		return goir.TVoid
	}
	at := l.exprType(e.Args[0])
	l.expr(e.Args[0])
	switch at.Kind {
	case goir.KString:
		l.emit(goir.Op{Code: goir.OpStrLen})
	case goir.KSlice:
		l.emit(goir.Op{Code: goir.OpSliceLen})
	case goir.KMap:
		l.emit(goir.Op{Code: goir.OpMapLen})
	case goir.KChan:
		l.emit(goir.Op{Code: goir.OpChanLen})
	default:
		l.fail(e.Pos(), "len (only strings, slices, maps and channels are supported)")
	}
	return goir.TInt64
}

// capCall lowers cap(s) for slices.
func (l *funcLowerer) capCall(e *ast.CallExpr) goir.Type {
	if len(e.Args) != 1 {
		l.fail(e.Pos(), "cap")
		return goir.TVoid
	}
	at := l.exprType(e.Args[0])
	if at.Kind == goir.KChan {
		l.expr(e.Args[0])
		l.emit(goir.Op{Code: goir.OpChanCap})
		return goir.TInt64
	}
	if at.Kind != goir.KSlice {
		l.fail(e.Pos(), "cap (only cap of slices and channels is supported)")
		return goir.TVoid
	}
	l.expr(e.Args[0])
	l.emit(goir.Op{Code: goir.OpSliceCap})
	return goir.TInt64
}

// printCall lowers println/print by building an object[] of boxed arguments.
func (l *funcLowerer) printCall(e *ast.CallExpr, isPrintln bool) goir.Type {
	l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(len(e.Args))})
	l.emit(goir.Op{Code: goir.OpNewObjArray})
	for i, a := range e.Args {
		l.emit(goir.Op{Code: goir.OpDup})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		at := l.exprType(a)
		l.expr(a)
		// Value types (int/uint/float/bool, GoString, structs, slices) must be
		// boxed before being stored into the object[] passed to Println/Print.
		l.emitBox(at)
		l.emit(goir.Op{Code: goir.OpStelemRef})
	}
	if isPrintln {
		l.emit(goir.Op{Code: goir.OpCallPrintln})
	} else {
		l.emit(goir.Op{Code: goir.OpCallPrint})
	}
	return goir.TVoid
}

// emitCallArgs lowers call arguments, packing the trailing arguments of a
// variadic callee into a slice (unless spread with `args...`).
func (l *funcLowerer) emitCallArgs(args []ast.Expr, params []goir.Type, variadic, ellipsis bool) {
	// f(g()) — a single argument that is itself a multi-result call: evaluate the
	// tuple once and spread its elements across the parameters (and the variadic
	// tail, if any). This is the only form Go allows mixing a multi-value call with.
	if len(args) == 1 && !ellipsis {
		if tup, ok := l.pkg.TypesInfo.TypeOf(args[0]).(*types.Tuple); ok && tup.Len() > 1 {
			tmp := l.addLocal(nil, goir.TObjectArray)
			l.expr(args[0]) // object[] tuple
			l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
			ldElem := func(i int) {
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
				l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
				l.emit(goir.Op{Code: goir.OpLdElemRef})
			}
			nFixed := len(params)
			if variadic {
				nFixed = len(params) - 1
			}
			for i := 0; i < nFixed; i++ {
				ldElem(i)
				l.emitUnbox(params[i])
			}
			if variadic {
				// Pack the remaining tuple elements (already boxed) into the slice.
				rest := tup.Len() - nFixed
				sliceTmp := l.addLocal(nil, params[nFixed])
				l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(rest)})
				l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(rest)})
				l.emitBoxedZero(*params[nFixed].Elem)
				l.emit(goir.Op{Code: goir.OpSliceMake})
				l.emit(goir.Op{Code: goir.OpStLoc, Local: sliceTmp})
				for k := 0; k < rest; k++ {
					l.emit(goir.Op{Code: goir.OpLdLoc, Local: sliceTmp})
					l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(k)})
					ldElem(nFixed + k) // element is already boxed
					l.emit(goir.Op{Code: goir.OpSliceSet})
				}
				l.emit(goir.Op{Code: goir.OpLdLoc, Local: sliceTmp})
			}
			return
		}
	}
	if !variadic {
		for i, a := range args {
			if i < len(params) {
				l.exprCoerced(a, params[i])
			} else {
				l.expr(a)
			}
		}
		return
	}
	nFixed := len(params) - 1
	for i := 0; i < nFixed; i++ {
		l.exprCoerced(args[i], params[i])
	}
	elemType := *params[nFixed].Elem
	if ellipsis {
		l.expr(args[nFixed]) // f(a, slice...) — the slice is passed directly
		return
	}
	l.packVariadic(args[nFixed:], elemType)
}

// packVariadic collects arguments into a fresh GoSlice (the variadic parameter).
func (l *funcLowerer) packVariadic(args []ast.Expr, elemType goir.Type) {
	tmp := l.addLocal(nil, goir.SliceType(elemType))
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(len(args))})
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(len(args))})
	l.emitBoxedZero(elemType)
	l.emit(goir.Op{Code: goir.OpSliceMake})
	l.emit(goir.Op{Code: goir.OpStLoc, Local: tmp})
	for i, a := range args {
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(i)})
		// Into a variadic ...any / ...interface (e.g. fmt's args), tag named values
		// with their identity so fmt can dispatch String()/Error() and print %T.
		l.emitBoxedElemInto(a, elemType)
		l.emit(goir.Op{Code: goir.OpSliceSet})
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
}

// exprType returns the goir.Type of an expression without emitting code.
func (l *funcLowerer) exprType(e ast.Expr) goir.Type {
	t, ok := l.goType(l.pkg.TypesInfo.TypeOf(e))
	if !ok {
		l.fail(e.Pos(), "type of expression")
		return goir.TInt64
	}
	return t
}

func (l *funcLowerer) emitZero(t goir.Type) {
	switch t.Kind {
	case goir.KInt64, goir.KUint64:
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: 0})
	case goir.KFloat64:
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: 0})
	case goir.KFloat32:
		l.emit(goir.Op{Code: goir.OpLdcR4, Float: 0})
	case goir.KString:
		l.emit(goir.Op{Code: goir.OpStrConst, Str: ""})
	default:
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0}) // int32/uint32/bool
	}
}

func (l *funcLowerer) emitInt(v int64, t goir.Type) {
	if t.Kind == goir.KInt32 || t.Kind == goir.KUint32 {
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: v})
		return
	}
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: v})
}

// emitArith emits the operator for two operands of operandType already on the
// stack, picking the representation-correct opcode: string concat, complex
// helpers, unsigned division/shift variants, or the plain integer/float op.
func (l *funcLowerer) emitArith(tok token.Token, operandType goir.Type) {
	switch operandType.Kind {
	case goir.KString:
		if tok == token.ADD {
			l.emit(goir.Op{Code: goir.OpStrConcat})
			return
		}
	case goir.KComplex:
		switch tok {
		case token.ADD:
			l.emit(goir.Op{Code: goir.OpComplexAdd})
		case token.SUB:
			l.emit(goir.Op{Code: goir.OpComplexSub})
		case token.MUL:
			l.emit(goir.Op{Code: goir.OpComplexMul})
		case token.QUO:
			l.emit(goir.Op{Code: goir.OpComplexDiv})
		}
		return
	}
	// a &^ b (bit clear) == a & ^b : complement the second operand, then AND.
	if tok == token.AND_NOT {
		l.emitInt(-1, operandType)
		l.emit(goir.Op{Code: goir.OpXor})
		l.emit(goir.Op{Code: goir.OpAnd})
		l.truncateInt(operandType)
		return
	}
	op, ok := arithOp(tok)
	if !ok {
		l.fail(0, "operator "+tok.String())
		return
	}
	if isUnsigned(operandType) {
		switch op {
		case goir.OpDiv:
			op = goir.OpDivUn
		case goir.OpRem:
			op = goir.OpRemUn
		case goir.OpShr:
			op = goir.OpShrUn
		}
	}
	if (op == goir.OpDiv || op == goir.OpRem || op == goir.OpDivUn || op == goir.OpRemUn) && isIntegerKind(operandType.Kind) {
		l.emitDivByZeroCheck()
	}
	l.emit(goir.Op{Code: op})
	l.truncateInt(operandType)
}

// isIntegerKind reports whether k is one of the integer representations (signed
// or unsigned, any width). Sub-word Go integers map to KInt32/KUint32.
func isIntegerKind(k goir.Kind) bool {
	switch k {
	case goir.KInt64, goir.KInt32, goir.KUint64, goir.KUint32:
		return true
	}
	return false
}

// emitDivByZeroCheck guards an integer division/remainder: with [dividend,
// divisor] on the stack it panics (Go's recoverable "integer divide by zero"
// runtime error) when the divisor is zero, otherwise leaves the stack unchanged
// for the div/rem op. Float division is exempt (Go yields ±Inf/NaN there).
func (l *funcLowerer) emitDivByZeroCheck() {
	ok := l.label()
	l.emit(goir.Op{Code: goir.OpDup})
	l.emit(goir.Op{Code: goir.OpBrTrue, Label: ok})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: "runtime error: integer divide by zero"})
	l.emit(goir.Op{Code: goir.OpBox, BoxTy: goir.TString}) // GoString is a value type; Panic takes object
	l.emit(goir.Op{Code: goir.OpCallPanic})
	l.mark(ok)
}

// truncateInt wraps an arithmetic result back to a sub-word integer's width so
// overflow matches Go (int8 127+1 == -128, byte 200+100 == 44).
func (l *funcLowerer) truncateInt(t goir.Type) {
	if t.TruncOp != goir.OpNop {
		l.emit(goir.Op{Code: t.TruncOp})
	}
}

// maxMinCall lowers the Go 1.21 builtin max/min over two or more ordered operands
// of the same type. It folds the arguments through a running accumulator. For
// floating-point operands a NaN argument propagates to the result (Go's rule:
// "if any argument is a NaN, the result is a NaN").
func (l *funcLowerer) maxMinCall(e *ast.CallExpr, isMax bool) goir.Type {
	t := l.exprType(e.Args[0])
	acc := l.addLocal(nil, t)
	cur := l.addLocal(nil, t)
	l.exprCoerced(e.Args[0], t)
	l.emit(goir.Op{Code: goir.OpStLoc, Local: acc})

	op := token.LSS
	if isMax {
		op = token.GTR
	}
	isFloat := t.Kind == goir.KFloat64 || t.Kind == goir.KFloat32
	for _, arg := range e.Args[1:] {
		l.exprCoerced(arg, t)
		l.emit(goir.Op{Code: goir.OpStLoc, Local: cur})
		replace, skip := l.label(), l.label()
		// if cur <op> acc -> replace
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: cur})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: acc})
		l.compare(op, t)
		l.emit(goir.Op{Code: goir.OpBrTrue, Label: replace})
		if isFloat {
			// cur is NaN (cur != cur) -> replace, so a NaN propagates.
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: cur})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: cur})
			l.emit(goir.Op{Code: goir.OpCeq})
			l.emit(goir.Op{Code: goir.OpBrFalse, Label: replace})
		}
		l.emit(goir.Op{Code: goir.OpBr, Label: skip})
		l.mark(replace)
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: cur})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: acc})
		l.mark(skip)
	}
	l.emit(goir.Op{Code: goir.OpLdLoc, Local: acc})
	return t
}

// clearCall lowers the clear builtin: clear(m) empties a map; clear(s) zeroes the
// elements of a slice.
func (l *funcLowerer) clearCall(e *ast.CallExpr) goir.Type {
	arg := e.Args[0]
	t := l.exprType(arg)
	switch t.Kind {
	case goir.KMap:
		l.expr(arg)
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ClearMap",
			Params: []goir.Type{t}, Ret: goir.TVoid,
		}})
	case goir.KSlice:
		l.expr(arg)
		l.emitBoxedZero(*t.Elem)
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "ClearSlice",
			Params: []goir.Type{t, goir.TObject}, Ret: goir.TVoid,
		}})
	default:
		l.fail(e.Pos(), "clear (only maps and slices are supported)")
	}
	return goir.TVoid
}

func arithOp(tok token.Token) (goir.Opcode, bool) {
	switch tok {
	case token.ADD:
		return goir.OpAdd, true
	case token.SUB:
		return goir.OpSub, true
	case token.MUL:
		return goir.OpMul, true
	case token.QUO:
		return goir.OpDiv, true
	case token.REM:
		return goir.OpRem, true
	case token.AND:
		return goir.OpAnd, true
	case token.OR:
		return goir.OpOr, true
	case token.XOR:
		return goir.OpXor, true
	case token.SHL:
		return goir.OpShl, true
	case token.SHR:
		return goir.OpShr, true
	default:
		return 0, false
	}
}

func b2i(b bool) int64 {
	if b {
		return 1
	}
	return 0
}
