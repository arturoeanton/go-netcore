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
	// Constant folding handles literals, named constants, iota, and constant
	// sub-expressions uniformly.
	if tv := l.pkg.TypesInfo.Types[e]; tv.Value != nil {
		l.emitConst(e, tv.Type, tv.Value)
		return
	}
	switch e := e.(type) {
	case *ast.Ident:
		if e.Name == "nil" {
			l.emit(goir.Op{Code: goir.OpLdNull})
			return
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
	if bt.Kind != goir.KStruct {
		l.fail(e.Pos(), "selector (only struct field access is supported)")
		return
	}
	fi := bt.Struct.FieldIndex(e.Sel.Name)
	if fi < 0 {
		l.fail(e.Pos(), "unknown field "+e.Sel.Name)
		return
	}
	l.expr(e.X)
	l.emit(goir.Op{Code: goir.OpLdFld, Struct: bt.Struct, Field: fi})
}

// compositeLit lowers a struct composite literal Point{...} (keyed or positional)
// into a temp local: zero-initialize, set provided fields, then push the value.
func (l *funcLowerer) compositeLit(e *ast.CompositeLit) goir.Type {
	t := l.exprType(e)
	switch t.Kind {
	case goir.KStruct:
		// handled below
	case goir.KSlice:
		return l.sliceLit(e, t)
	case goir.KMap:
		return l.mapLit(e, t)
	default:
		l.fail(e.Pos(), "composite literal (only struct, slice and map literals are supported)")
		return goir.TVoid
	}
	s := t.Struct
	tmp := l.addLocal(nil, t)
	l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
	l.emit(goir.Op{Code: goir.OpInitObj, Struct: s})

	for i, elt := range e.Elts {
		var fi int
		var val ast.Expr
		if kv, ok := elt.(*ast.KeyValueExpr); ok {
			key, ok := kv.Key.(*ast.Ident)
			if !ok {
				l.fail(kv.Pos(), "composite literal key")
				return goir.TVoid
			}
			fi = s.FieldIndex(key.Name)
			val = kv.Value
		} else {
			fi = i // positional
			val = elt
		}
		if fi < 0 || fi >= len(s.Fields) {
			l.fail(elt.Pos(), "composite literal field")
			return goir.TVoid
		}
		l.emit(goir.Op{Code: goir.OpLdLocA, Local: tmp})
		l.expr(val)
		l.emit(goir.Op{Code: goir.OpStFld, Struct: s, Field: fi})
	}

	l.emit(goir.Op{Code: goir.OpLdLoc, Local: tmp})
	return t
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
	switch gt {
	case goir.TInt64:
		iv, _ := constant.Int64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: iv})
	case goir.TInt32:
		iv, _ := constant.Int64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: iv})
	case goir.TUint64:
		uv, _ := constant.Uint64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(uv)})
	case goir.TUint32:
		uv, _ := constant.Uint64Val(constant.ToInt(v))
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(uv)})
	case goir.TFloat64:
		fv, _ := constant.Float64Val(constant.ToFloat(v))
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: fv})
	case goir.TFloat32:
		fv, _ := constant.Float64Val(constant.ToFloat(v))
		l.emit(goir.Op{Code: goir.OpLdcR4, Float: fv})
	case goir.TComplex:
		cv := constant.ToComplex(v)
		re, _ := constant.Float64Val(constant.Real(cv))
		im, _ := constant.Float64Val(constant.Imag(cv))
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: re})
		l.emit(goir.Op{Code: goir.OpLdcR8, Float: im})
		l.emit(goir.Op{Code: goir.OpComplexMake})
	case goir.TBool:
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: b2i(constant.BoolVal(v))})
	case goir.TString:
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
		l.expr(e.X)
		l.expr(e.Y)
		l.compare(e.Op, opType)
		return
	}

	// Arithmetic / bitwise / string-concat / complex (operand-type directed).
	if _, ok := arithOp(e.Op); !ok {
		l.fail(e.Pos(), "operator "+e.Op.String())
		return
	}
	opType := l.exprType(e.X)
	l.expr(e.X)
	l.expr(e.Y)
	l.emitArith(e.Op, opType)
}

func isUnsigned(t goir.Type) bool {
	return t.Kind == goir.KUint64 || t.Kind == goir.KUint32
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
	if operandType.Kind == goir.KPtr || operandType.Kind == goir.KMap || operandType.Kind == goir.KObject {
		// Reference equality (pointers, maps-vs-nil, interface-vs-nil).
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

// callExpr lowers a call and returns its result type (TVoid for none).
func (l *funcLowerer) callExpr(e *ast.CallExpr) goir.Type {
	// Type conversions: T(x), including []byte(s) / []rune(s) where the call
	// target is a type expression rather than a plain identifier.
	if tv, ok := l.pkg.TypesInfo.Types[e.Fun]; ok && tv.IsType() {
		return l.conversion(e)
	}
	// Method calls: recv.Method(args).
	if sel, ok := e.Fun.(*ast.SelectorExpr); ok {
		if seln := l.pkg.TypesInfo.Selections[sel]; seln != nil && seln.Kind() == types.MethodVal {
			return l.methodCall(e, sel, seln)
		}
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
		default:
			l.fail(e.Pos(), "builtin "+o.Name())
			return goir.TVoid
		}
	case *types.TypeName:
		// Type conversion, e.g. int64(x), int32(x), rune(x).
		return l.conversion(e)
	case *types.Func:
		callee, ok := l.byName[fun.Name]
		if !ok {
			// A generic function is monomorphized per concrete instantiation.
			callee, ok = l.genericCallee(fun)
			if !ok {
				l.fail(e.Pos(), "call to "+fun.Name)
				return goir.TVoid
			}
		}
		l.emitCallArgs(e.Args, callee.Params, o.Type().(*types.Signature).Variadic(), e.Ellipsis.IsValid())
		l.emit(goir.Op{Code: goir.OpCallMethod, Callee: callee})
		return callee.Ret
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
	target, ok := l.goType(l.pkg.TypesInfo.TypeOf(e))
	if !ok {
		l.fail(e.Pos(), "conversion target type")
		return goir.TVoid
	}
	// []byte(s) / []rune(s).
	if target.Kind == goir.KSlice && l.exprType(e.Args[0]) == goir.TString {
		return l.strToSliceConversion(e, target)
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
	return target
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
		l.emitBoxedElem(a)
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
	if t == goir.TInt32 {
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
	l.emit(goir.Op{Code: op})
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
