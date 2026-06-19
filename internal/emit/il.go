package emit

import (
	"encoding/binary"
	"math"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// tokenSet holds the metadata tokens the IL translator needs. Value-type tokens
// are used only by `box`; method signatures encode primitives inline.
type tokenSet struct {
	object   uint32 // TypeRef System.Object (newarr element)
	int64Box uint32 // TypeRef System.Int64
	int32Box uint32 // TypeRef System.Int32
	boolBox  uint32 // TypeRef System.Boolean
	println    uint32 // MemberRef Builtins.Println(object[])
	print      uint32 // MemberRef Builtins.Print(object[])
	method     func(*goir.Method) uint32
	us         map[string]uint16 // #US offsets for ldstr
	structType func(*goir.Struct) uint32
	field      func(*goir.Struct, int) uint32
	invoke     *goir.Method // the closure dispatcher, for ldftn (goroutine setup)
}

type ilBuilder struct{ buf []byte }

func (b *ilBuilder) u8(v byte)     { b.buf = append(b.buf, v) }
func (b *ilBuilder) i32(v int32)   { b.buf = binary.LittleEndian.AppendUint32(b.buf, uint32(v)) }
func (b *ilBuilder) u32(v uint32)  { b.buf = binary.LittleEndian.AppendUint32(b.buf, v) }
func (b *ilBuilder) i64(v int64)   { b.buf = binary.LittleEndian.AppendUint64(b.buf, uint64(v)) }

// ldcI4 pushes a 32-bit constant with the shortest encoding.
func (b *ilBuilder) ldcI4(n int32) {
	switch {
	case n >= 0 && n <= 8:
		b.u8(byte(0x16 + n))
	case n == -1:
		b.u8(0x15)
	case n >= -128 && n <= 127:
		b.u8(0x1F)
		b.u8(byte(int8(n)))
	default:
		b.u8(0x20)
		b.i32(n)
	}
}

// branchFixup records a long-form branch whose target offset must be patched.
type branchFixup struct {
	operandPos int // byte offset of the 4-byte relative operand
	target     int // target label id
}

// translateMethod lowers a method's IR op stream to a CIL method body
// (header + code). Long-form branches are used throughout and patched after a
// single pass, since their size is fixed.
func translateMethod(m *goir.Method, tok tokenSet, localSigTok uint32) []byte {
	b := &ilBuilder{}
	labelPos := map[int]int{}
	var fixups []branchFixup

	branch := func(op byte, target int) {
		b.u8(op)
		fixups = append(fixups, branchFixup{operandPos: len(b.buf), target: target})
		b.i32(0) // placeholder
	}

	for _, op := range m.Code {
		switch op.Code {
		case goir.OpNop:
			b.u8(0x00)
		case goir.OpLdcI8:
			b.u8(0x21)
			b.i64(op.Int)
		case goir.OpLdcI4:
			b.ldcI4(int32(op.Int))
		case goir.OpLdcR8:
			b.u8(0x23)
			b.buf = binary.LittleEndian.AppendUint64(b.buf, math.Float64bits(op.Float))
		case goir.OpLdcR4:
			b.u8(0x22)
			b.buf = binary.LittleEndian.AppendUint32(b.buf, math.Float32bits(float32(op.Float)))
		case goir.OpLdStr:
			b.u8(0x72)
			b.u32(0x70000000 | uint32(tok.us[op.Str]))
		case goir.OpLdLoc:
			b.u8(0x11) // ldloc.s
			b.u8(byte(op.Local))
		case goir.OpStLoc:
			b.u8(0x13) // stloc.s
			b.u8(byte(op.Local))
		case goir.OpLdArg:
			b.u8(0x0E) // ldarg.s
			b.u8(byte(op.Arg))
		case goir.OpAdd:
			b.u8(0x58)
		case goir.OpSub:
			b.u8(0x59)
		case goir.OpMul:
			b.u8(0x5A)
		case goir.OpDiv:
			b.u8(0x5B)
		case goir.OpRem:
			b.u8(0x5D)
		case goir.OpNeg:
			b.u8(0x65)
		case goir.OpAnd:
			b.u8(0x5F)
		case goir.OpOr:
			b.u8(0x60)
		case goir.OpXor:
			b.u8(0x61)
		case goir.OpShl:
			b.u8(0x62)
		case goir.OpShr:
			b.u8(0x63)
		case goir.OpCeq:
			b.u8(0xFE)
			b.u8(0x01)
		case goir.OpClt:
			b.u8(0xFE)
			b.u8(0x04)
		case goir.OpCgt:
			b.u8(0xFE)
			b.u8(0x02)
		case goir.OpCltUn:
			b.u8(0xFE)
			b.u8(0x05) // clt.un
		case goir.OpCgtUn:
			b.u8(0xFE)
			b.u8(0x03) // cgt.un
		case goir.OpDivUn:
			b.u8(0x5C)
		case goir.OpRemUn:
			b.u8(0x5E)
		case goir.OpShrUn:
			b.u8(0x64)
		case goir.OpConvR8:
			b.u8(0x6C)
		case goir.OpConvR4:
			b.u8(0x6B)
		case goir.OpConvU8:
			b.u8(0x6E)
		case goir.OpConvU4:
			b.u8(0x6D)
		case goir.OpNot:
			b.ldcI4(0)
			b.u8(0xFE)
			b.u8(0x01) // ceq
		case goir.OpConvI8:
			b.u8(0x6A)
		case goir.OpConvI4:
			b.u8(0x69)
		case goir.OpNewObjArray:
			b.u8(0x8D)
			b.u32(tok.object)
		case goir.OpDup:
			b.u8(0x25)
		case goir.OpStelemRef:
			b.u8(0xA2)
		case goir.OpLdElemRef:
			b.u8(0x9A) // ldelem.ref (0xA3 is ldelem <token>)
		case goir.OpBox:
			b.u8(0x8C)
			b.u32(boxToken(tok, op.BoxTy))
		case goir.OpCallPrintln:
			b.u8(0x28)
			b.u32(tok.println)
		case goir.OpCallPrint:
			b.u8(0x28)
			b.u32(tok.print)
		case goir.OpCallPanic:
			b.u8(0x28)
			b.u32(tokPanic)
		case goir.OpCallRecover:
			b.u8(0x28)
			b.u32(tokRecover)
		case goir.OpCallSetPanic:
			b.u8(0x28)
			b.u32(tokSetPanic)
		case goir.OpCallPanicHandled:
			b.u8(0x28)
			b.u32(tokPanicHandled)
		case goir.OpRethrow:
			b.u8(0xFE)
			b.u8(0x1A)
		case goir.OpClosNew:
			b.u8(0x28)
			b.u32(tokClosNew)
		case goir.OpClosId:
			b.u8(0x28)
			b.u32(tokClosId)
		case goir.OpClosEnv:
			b.u8(0x28)
			b.u32(tokClosEnv)
		case goir.OpLeave:
			b.u8(0xDD)
			fixups = append(fixups, branchFixup{operandPos: len(b.buf), target: op.Label})
			b.i32(0)
		case goir.OpCallMethod:
			b.u8(0x28)
			b.u32(tok.method(op.Callee))
		case goir.OpStrConst:
			b.u8(0x72) // ldstr (System.String)
			b.u32(0x70000000 | uint32(tok.us[op.Str]))
			b.u8(0x28) // call GoStrings.FromLiteral -> GoString
			b.u32(tokStrFromLit)
		case goir.OpStrLen:
			b.u8(0x28)
			b.u32(tokStrLen)
		case goir.OpStrIndex:
			b.u8(0x28)
			b.u32(tokStrByteAt)
		case goir.OpStrConcat:
			b.u8(0x28)
			b.u32(tokStrConcat)
		case goir.OpStrEqual:
			b.u8(0x28)
			b.u32(tokStrEqual)
		case goir.OpStrCompare:
			b.u8(0x28)
			b.u32(tokStrCompare)
		case goir.OpStrRuneAt:
			b.u8(0x28)
			b.u32(tokStrRuneAt)
		case goir.OpStrRuneSize:
			b.u8(0x28)
			b.u32(tokStrRuneSize)
		case goir.OpLdLocA:
			b.u8(0x12) // ldloca.s
			b.u8(byte(op.Local))
		case goir.OpLdFld:
			b.u8(0x7B) // ldfld
			b.u32(tok.field(op.Struct, op.Field))
		case goir.OpLdFldA:
			b.u8(0x7C) // ldflda
			b.u32(tok.field(op.Struct, op.Field))
		case goir.OpStFld:
			b.u8(0x7D) // stfld
			b.u32(tok.field(op.Struct, op.Field))
		case goir.OpInitObj:
			b.u8(0xFE) // initobj
			b.u8(0x15)
			b.u32(tok.structType(op.Struct))
		case goir.OpSliceMake:
			b.u8(0x28)
			b.u32(tokSliceMake)
		case goir.OpSliceGet:
			b.u8(0x28)
			b.u32(tokSliceGet)
		case goir.OpSliceSet:
			b.u8(0x28)
			b.u32(tokSliceSet)
		case goir.OpSliceLen:
			b.u8(0x28)
			b.u32(tokSliceLen)
		case goir.OpSliceCap:
			b.u8(0x28)
			b.u32(tokSliceCap)
		case goir.OpSliceAppend:
			b.u8(0x28)
			b.u32(tokSliceApp)
		case goir.OpSliceSlice:
			b.u8(0x28)
			b.u32(tokSliceSlice)
		case goir.OpStrToBytes:
			b.u8(0x28)
			b.u32(tokStrToBytes)
		case goir.OpStrToRunes:
			b.u8(0x28)
			b.u32(tokStrToRunes)
		case goir.OpUnbox:
			b.u8(0xA5) // unbox.any (castclass for reference types)
			b.u32(boxToken(tok, op.BoxTy))
		case goir.OpIsInst:
			b.u8(0x75) // isinst
			b.u32(boxToken(tok, op.BoxTy))
		case goir.OpMapMake:
			b.u8(0x28)
			b.u32(tokMapMake)
		case goir.OpMapGet:
			b.u8(0x28)
			b.u32(tokMapGet)
		case goir.OpMapContains:
			b.u8(0x28)
			b.u32(tokMapContains)
		case goir.OpMapSet:
			b.u8(0x28)
			b.u32(tokMapSet)
		case goir.OpMapDelete:
			b.u8(0x28)
			b.u32(tokMapDelete)
		case goir.OpMapLen:
			b.u8(0x28)
			b.u32(tokMapLen)
		case goir.OpMapKeys:
			b.u8(0x28)
			b.u32(tokMapKeys)
		case goir.OpLdNull:
			b.u8(0x14) // ldnull
		case goir.OpPtrNew:
			b.u8(0x21) // ldc.i8 typeId (Op.Int; 0 = untyped)
			b.i64(op.Int)
			b.u8(0x28)
			b.u32(tokPtrNew)
		case goir.OpPtrTypeId:
			b.u8(0x28)
			b.u32(tokPtrTypeId)
		case goir.OpPtrGet:
			b.u8(0x28)
			b.u32(tokPtrGet)
		case goir.OpPtrSet:
			b.u8(0x28)
			b.u32(tokPtrSet)
		case goir.OpChanMake:
			b.u8(0x28)
			b.u32(tokChanMake)
		case goir.OpChanSend:
			b.u8(0x28)
			b.u32(tokChanSend)
		case goir.OpChanRecv:
			b.u8(0x28)
			b.u32(tokChanRecv)
		case goir.OpChanRecv2:
			b.u8(0x28)
			b.u32(tokChanRecv2)
		case goir.OpChanClose:
			b.u8(0x28)
			b.u32(tokChanClose)
		case goir.OpChanLen:
			b.u8(0x28)
			b.u32(tokChanLen)
		case goir.OpChanCap:
			b.u8(0x28)
			b.u32(tokChanCap)
		case goir.OpGoStart:
			b.u8(0x28)
			b.u32(tokGoRun)
		case goir.OpSelect:
			b.u8(0x28)
			b.u32(tokSelect)
		case goir.OpComplexMake:
			b.u8(0x28)
			b.u32(tokCplxMake)
		case goir.OpComplexAdd:
			b.u8(0x28)
			b.u32(tokCplxAdd)
		case goir.OpComplexSub:
			b.u8(0x28)
			b.u32(tokCplxSub)
		case goir.OpComplexMul:
			b.u8(0x28)
			b.u32(tokCplxMul)
		case goir.OpComplexDiv:
			b.u8(0x28)
			b.u32(tokCplxDiv)
		case goir.OpComplexNeg:
			b.u8(0x28)
			b.u32(tokCplxNeg)
		case goir.OpComplexEq:
			b.u8(0x28)
			b.u32(tokCplxEq)
		case goir.OpComplexReal:
			b.u8(0x28)
			b.u32(tokCplxReal)
		case goir.OpComplexImag:
			b.u8(0x28)
			b.u32(tokCplxImag)
		case goir.OpDeferMark:
			b.u8(0x28)
			b.u32(tokDeferMark)
		case goir.OpDeferPush:
			b.u8(0x28)
			b.u32(tokDeferPush)
		case goir.OpDeferRun:
			b.u8(0x28)
			b.u32(tokDeferRun)
		case goir.OpRegisterInvoker:
			// GoRuntime.SetInvoker(new GoInvoker(Program.__invoke)):
			//   ldnull; ldftn __invoke; newobj GoInvoker::.ctor; call SetInvoker
			b.u8(0x14) // ldnull
			b.u8(0xFE) // ldftn
			b.u8(0x06)
			b.u32(tok.method(tok.invoke))
			b.u8(0x73) // newobj
			b.u32(tokInvokerCtor)
			b.u8(0x28) // call
			b.u32(tokSetInvoker)
		case goir.OpLabel:
			labelPos[op.Label] = len(b.buf)
		case goir.OpBr:
			branch(0x38, op.Label)
		case goir.OpBrTrue:
			branch(0x3A, op.Label)
		case goir.OpBrFalse:
			branch(0x39, op.Label)
		case goir.OpRet:
			b.u8(0x2A)
		case goir.OpPop:
			b.u8(0x26)
		}
	}

	// Patch branch targets: offset is relative to the instruction following the
	// 4-byte operand.
	for _, fx := range fixups {
		target := labelPos[fx.target]
		rel := int32(target - (fx.operandPos + 4))
		binary.LittleEndian.PutUint32(b.buf[fx.operandPos:], uint32(rel))
	}

	// Build the exception-handling section (if any) from resolved label offsets.
	var eh []byte
	if len(m.EH) > 0 {
		eh = buildEHSection(m.EH, labelPos)
	}
	return methodBody(b.buf, maxStack(m), localSigTok, eh)
}

// buildEHSection encodes a fat exception-handling section: one catch clause per
// EH region, catching GoCLR.Runtime.GoPanicException.
func buildEHSection(clauses []goir.EHClause, labelPos map[int]int) []byte {
	size := 4 + 24*len(clauses)
	out := make([]byte, 0, size)
	out = append(out, 0x41)                            // EHTable | FatFormat
	out = append(out, byte(size), byte(size>>8), byte(size>>16)) // 3-byte DataSize
	for _, c := range clauses {
		tryOff := labelPos[c.TryStart]
		tryLen := labelPos[c.TryEnd] - tryOff
		hOff := labelPos[c.HandlerStart]
		hLen := labelPos[c.HandlerEnd] - hOff
		out = binary.LittleEndian.AppendUint32(out, 0) // Flags = exception (catch)
		out = binary.LittleEndian.AppendUint32(out, uint32(tryOff))
		out = binary.LittleEndian.AppendUint32(out, uint32(tryLen))
		out = binary.LittleEndian.AppendUint32(out, uint32(hOff))
		out = binary.LittleEndian.AppendUint32(out, uint32(hLen))
		out = binary.LittleEndian.AppendUint32(out, tokGoPanic) // catch type
	}
	return out
}

func boxToken(tok tokenSet, t goir.Type) uint32 {
	switch t {
	case goir.TInt64:
		return tok.int64Box
	case goir.TInt32:
		return tok.int32Box
	case goir.TUint64:
		return tokUInt64
	case goir.TUint32:
		return tokUInt32
	case goir.TFloat64:
		return tokDouble
	case goir.TFloat32:
		return tokSingle
	case goir.TBool:
		return tok.boolBox
	case goir.TString:
		return tokGoString
	default:
		if t.Kind == goir.KStruct {
			return tok.structType(t.Struct)
		}
		if t.Kind == goir.KSlice {
			return tokGoSlice
		}
		if t.Kind == goir.KMap {
			return tokGoMap
		}
		if t.Kind == goir.KPtr {
			return tokGoPtr
		}
		if t.Kind == goir.KFunc {
			return tokGoClosure
		}
		return tok.object
	}
}

// methodBody wraps code in a tiny or fat header. A fat header is required when
// the method has locals (to carry LocalVarSigTok), when the code is large, or
// when maxStack exceeds 8.
func methodBody(code []byte, maxStack int, localSigTok uint32, eh []byte) []byte {
	if localSigTok == 0 && maxStack <= 8 && len(code) < 64 && len(eh) == 0 {
		return append([]byte{byte(len(code)<<2) | 0x02}, code...)
	}
	hdr := make([]byte, 12)
	flags := uint16(0x3003) // fat, 3 dwords
	if len(eh) > 0 {
		flags |= 0x0008 // CorILMethod_MoreSects
	}
	binary.LittleEndian.PutUint16(hdr[0:], flags)
	if maxStack < 8 {
		maxStack = 8
	}
	binary.LittleEndian.PutUint16(hdr[2:], uint16(maxStack))
	binary.LittleEndian.PutUint32(hdr[4:], uint32(len(code)))
	binary.LittleEndian.PutUint32(hdr[8:], localSigTok)
	body := append(hdr, code...)
	if len(eh) > 0 {
		for len(body)%4 != 0 { // the EH section must be 4-byte aligned
			body = append(body, 0)
		}
		body = append(body, eh...)
	}
	return body
}

// maxStack computes an upper bound on evaluation-stack depth via a linear scan
// over the (structured) instruction stream. Labels are NOT assumed to be
// stack-empty: control-flow constructs may be used as sub-expressions (e.g. an
// interface dispatch as a call argument), so the running depth is carried across
// labels. Branch targets in the generated code are always reached at a depth
// equal to the linear fall-through depth, so this bound is exact for our codegen.
func maxStack(m *goir.Method) int {
	cur, max := 0, 0
	bump := func(d int) {
		cur += d
		if cur < 0 {
			cur = 0
		}
		if cur > max {
			max = cur
		}
	}
	for _, op := range m.Code {
		switch op.Code {
		case goir.OpLdcI8, goir.OpLdcI4, goir.OpLdcR8, goir.OpLdcR4, goir.OpLdStr, goir.OpLdLoc,
			goir.OpLdArg, goir.OpDup, goir.OpStrConst, goir.OpLdLocA, goir.OpMapMake, goir.OpLdNull,
			goir.OpCallRecover, goir.OpCallPanicHandled, goir.OpDeferMark:
			bump(+1)
		case goir.OpStLoc, goir.OpPop, goir.OpAdd, goir.OpSub, goir.OpMul, goir.OpDiv,
			goir.OpRem, goir.OpAnd, goir.OpOr, goir.OpXor, goir.OpShl, goir.OpShr,
			goir.OpCeq, goir.OpClt, goir.OpCgt, goir.OpBrTrue, goir.OpBrFalse,
			goir.OpStrIndex, goir.OpStrConcat, goir.OpStrEqual, goir.OpStrCompare,
			goir.OpStrRuneAt, goir.OpStrRuneSize, goir.OpInitObj,
			goir.OpSliceGet, goir.OpSliceAppend, goir.OpMapContains, goir.OpLdElemRef,
			goir.OpDivUn, goir.OpRemUn, goir.OpShrUn, goir.OpCltUn, goir.OpCgtUn,
			goir.OpChanClose, goir.OpGoStart, goir.OpDeferPush, goir.OpDeferRun,
			goir.OpComplexMake, goir.OpComplexAdd, goir.OpComplexSub, goir.OpComplexMul,
			goir.OpComplexDiv, goir.OpComplexEq:
			bump(-1)
		case goir.OpStFld, goir.OpSliceMake, goir.OpSliceSlice, goir.OpMapGet, goir.OpMapDelete,
			goir.OpPtrSet, goir.OpClosNew, goir.OpChanSend:
			bump(-2)
		case goir.OpRegisterInvoker:
			bump(+2) // ldnull + ldftn, consumed by newobj
			bump(-2)
		case goir.OpSliceSet, goir.OpMapSet:
			bump(-3)
		case goir.OpSelect:
			bump(-3) // pops chans, ops, sendVals, hasDefault; pushes object[]
		case goir.OpNot:
			bump(+1) // ldc.0 then ceq
			bump(-1)
		case goir.OpStelemRef:
			bump(-3)
		case goir.OpCallPrintln, goir.OpCallPrint, goir.OpCallPanic, goir.OpCallSetPanic:
			bump(-1) // pops its single argument
		case goir.OpCallMethod:
			d := -len(op.Callee.Params)
			if op.Callee.Ret != goir.TVoid {
				d++
			}
			bump(d)
		case goir.OpRet:
			if m.Ret != goir.TVoid {
				bump(-1)
			}
		}
	}
	if len(m.EH) > 0 {
		max++ // the runtime pushes the exception at each handler entry
	}
	return max + 2 // small margin
}
