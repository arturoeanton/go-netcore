// Package emit writes a managed .NET PE assembly (ECMA-335) from GoCLR IR.
//
// It emits one static method per Go function onto a single `Program` type, with
// typed signatures, locals (via StandAloneSig), control flow, and calls into the
// GoCLR runtime. The metadata writer assigns tokens dynamically so the set of
// methods/types can grow with later milestones.
package emit

import (
	"os"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

const (
	runtimeAssembly  = "GoCLR.Runtime"
	runtimeNamespace = "GoCLR.Runtime"
)

// Fixed metadata tokens (rows assigned deterministically in tables.go).
const (
	tokObject   uint32 = 0x01000001 // TypeRef System.Object
	tokInt64    uint32 = 0x01000002 // TypeRef System.Int64
	tokBoolean  uint32 = 0x01000003 // TypeRef System.Boolean
	tokInt32    uint32 = 0x01000004 // TypeRef System.Int32
	tokGoString uint32 = 0x01000006 // TypeRef GoCLR.Runtime.GoString (for box)
	tokPrintln  uint32 = 0x0A000001 // MemberRef Builtins.Println
	tokPrint    uint32 = 0x0A000002 // MemberRef Builtins.Print
	// GoStrings.* MemberRefs (rows 3..10).
	tokStrFromLit  uint32 = 0x0A000003
	tokStrLen      uint32 = 0x0A000004
	tokStrByteAt   uint32 = 0x0A000005
	tokStrConcat   uint32 = 0x0A000006
	tokStrEqual    uint32 = 0x0A000007
	tokStrCompare  uint32 = 0x0A000008
	tokStrRuneAt   uint32 = 0x0A000009
	tokStrRuneSize uint32 = 0x0A00000A
	// String<->slice conversions (GoStrings) and GoSlices.* (rows 11..19).
	tokStrToBytes uint32 = 0x0A00000B
	tokStrToRunes uint32 = 0x0A00000C
	tokSliceMake  uint32 = 0x0A00000D
	tokSliceGet   uint32 = 0x0A00000E
	tokSliceSet   uint32 = 0x0A00000F
	tokSliceLen   uint32 = 0x0A000010
	tokSliceCap   uint32 = 0x0A000011
	tokSliceApp   uint32 = 0x0A000012
	tokSliceSlice uint32 = 0x0A000013
	tokGoSlice    uint32 = 0x01000009 // TypeRef GoCLR.Runtime.GoSlice (box/unbox/sig)
	// GoMaps.* MemberRefs (rows 20..26).
	tokMapMake     uint32 = 0x0A000014
	tokMapLen      uint32 = 0x0A000015
	tokMapGet      uint32 = 0x0A000016
	tokMapContains uint32 = 0x0A000017
	tokMapSet      uint32 = 0x0A000018
	tokMapDelete   uint32 = 0x0A000019
	tokMapKeys     uint32 = 0x0A00001A
	tokGoMap       uint32 = 0x0100000B // TypeRef GoCLR.Runtime.GoMap (sig/castclass)
	// GoPtrs.* MemberRefs (rows 27..29).
	tokPtrNew uint32 = 0x0A00001B
	tokPtrGet uint32 = 0x0A00001C
	tokPtrSet uint32 = 0x0A00001D
	tokGoPtr  uint32 = 0x0100000D // TypeRef GoCLR.Runtime.GoPtr (sig/castclass)
	tokPanic        uint32 = 0x0A00001E // MemberRef Builtins.Panic(object) (row 30)
	tokRecover      uint32 = 0x0A00001F // Builtins.Recover() (row 31)
	tokSetPanic     uint32 = 0x0A000020 // Builtins.SetPanic(GoPanicException) (row 32)
	tokPanicHandled uint32 = 0x0A000021 // Builtins.PanicHandled() (row 33)
	tokGoPanic      uint32 = 0x0100000F // TypeRef GoCLR.Runtime.GoPanicException (catch class)
	// GoClosures.* MemberRefs (rows 34..36).
	tokClosNew   uint32 = 0x0A000022
	tokClosId    uint32 = 0x0A000023
	tokClosEnv   uint32 = 0x0A000024
	tokGoClosure uint32 = 0x01000010 // TypeRef GoCLR.Runtime.GoClosure (sig/castclass)
	// System.* numeric box typerefs (rows 18..21).
	tokDouble uint32 = 0x01000012
	tokSingle uint32 = 0x01000013
	tokUInt64 uint32 = 0x01000014
	tokUInt32 uint32 = 0x01000015
	// GoChans.* MemberRefs (rows 37..43) + GoChan TypeRef (row 22).
	tokChanMake  uint32 = 0x0A000025
	tokChanSend  uint32 = 0x0A000026
	tokChanRecv  uint32 = 0x0A000027
	tokChanRecv2 uint32 = 0x0A000028
	tokChanClose uint32 = 0x0A000029
	tokChanLen   uint32 = 0x0A00002A
	tokChanCap   uint32 = 0x0A00002B
	// GoRuntime.* MemberRefs (rows 44..45) + GoInvoker .ctor (row 46).
	tokGoRun       uint32 = 0x0A00002C // GoRuntime.Go(GoClosure)
	tokSetInvoker  uint32 = 0x0A00002D // GoRuntime.SetInvoker(GoInvoker)
	tokInvokerCtor uint32 = 0x0A00002E // GoInvoker::.ctor(object, native int)
	tokSelect      uint32 = 0x0A00002F // GoSelect.Select(object[],object[],object[],bool) (row 47)
	tokPtrTypeId   uint32 = 0x0A000030 // GoPtrs.TypeIdOf(GoPtr) -> i8 (row 48)
	// GoComplexs.* MemberRefs (rows 49..57) + GoComplex TypeRef (row 27).
	tokCplxMake uint32 = 0x0A000031
	tokCplxAdd  uint32 = 0x0A000032
	tokCplxSub  uint32 = 0x0A000033
	tokCplxMul  uint32 = 0x0A000034
	tokCplxDiv  uint32 = 0x0A000035
	tokCplxNeg  uint32 = 0x0A000036
	tokCplxEq   uint32 = 0x0A000037
	tokCplxReal uint32 = 0x0A000038
	tokCplxImag uint32 = 0x0A000039
	// GoDefers.* MemberRefs (rows 58..60) + GoDefers TypeRef (row 29).
	tokDeferMark uint32 = 0x0A00003A
	tokDeferPush uint32 = 0x0A00003B
	tokDeferRun  uint32 = 0x0A00003C
	methodBase uint32 = 0x06000000
	sigBase        uint32 = 0x11000000
	typeDefBase    uint32 = 0x02000000
	fieldTableBase uint32 = 0x04000000
)

// mvid is a fixed module version GUID for deterministic output.
var mvid = [16]byte{0x47, 0x6f, 0x43, 0x4c, 0x52, 0x4d, 0x31, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01}

const textRVA = 0x2000

// Emit writes the program as a .NET assembly to outPath.
func Emit(prog *goir.Program, outPath string) error {
	h := newHeaps()

	// Intern user strings first so ldstr offsets are stable.
	usOff := map[string]uint16{}
	for _, m := range prog.Methods {
		for _, op := range m.Code {
			if op.Code == goir.OpLdStr || op.Code == goir.OpStrConst {
				if _, ok := usOff[op.Str]; !ok {
					usOff[op.Str] = h.addUserString(op.Str)
				}
			}
		}
	}

	// Assign struct TypeDef rows (after <Module>=1, Program=2) and field rows.
	// s.TypeDefRow is read back by appendTypeSig when encoding struct signatures.
	// Globals occupy the first Field rows (static fields on Program); struct
	// fields follow.
	fieldBase := map[*goir.Struct]int{}
	nextField := 1 + len(prog.Globals)
	for i, s := range prog.Structs {
		s.TypeDefRow = 3 + i
		fieldBase[s] = nextField
		nextField += len(s.Fields)
	}

	// Assign method tokens.
	methodTok := map[*goir.Method]uint32{}
	for i, m := range prog.Methods {
		methodTok[m] = methodBase + uint32(i+1)
	}

	// Assign StandAloneSig rows for methods with locals.
	localSigTok := map[*goir.Method]uint32{}
	var sigBlobOffsets []uint16
	for _, m := range prog.Methods {
		if len(m.Locals) == 0 {
			continue
		}
		off := h.addBlob(localVarSigBlob(m.Locals))
		sigBlobOffsets = append(sigBlobOffsets, off)
		localSigTok[m] = sigBase + uint32(len(sigBlobOffsets))
	}

	// Collect external (shim) references and assign their dynamic tokens.
	externs := collectExterns(prog)

	tok := tokenSet{
		object:   tokObject,
		int64Box: tokInt64,
		int32Box: tokInt32,
		boolBox:  tokBoolean,
		println:  tokPrintln,
		print:    tokPrint,
		method:   func(m *goir.Method) uint32 { return methodTok[m] },
		us:       usOff,
		structType: func(s *goir.Struct) uint32 {
			return typeDefBase | uint32(s.TypeDefRow)
		},
		field: func(s *goir.Struct, idx int) uint32 {
			return fieldTableBase | uint32(fieldBase[s]+idx)
		},
		global: func(idx int) uint32 {
			return fieldTableBase | uint32(1+idx) // globals are the first Field rows
		},
		extern: externs.token,
		invoke: prog.Invoke,
	}

	// Build method bodies (position-independent: all tokens are fixed).
	bodies := make([][]byte, len(prog.Methods))
	for i, m := range prog.Methods {
		bodies[i] = translateMethod(m, tok, localSigTok[m])
	}

	// Lay out .text: CLI header, then each 4-aligned method body, then metadata.
	methodRVAs := make([]uint32, len(prog.Methods))
	off := cliHeaderSize
	for i, body := range bodies {
		off = roundUp(off, 4)
		methodRVAs[i] = uint32(textRVA + off)
		off += len(body)
	}
	metadataOffset := roundUp(off, 4)
	metadataRVA := uint32(textRVA + metadataOffset)

	tables := buildTables(prog, h, methodRVAs, sigBlobOffsets, externs)
	meta := buildMetadata(tables, h)

	entryToken := methodTok[prog.Entry]
	cli := buildCLIHeader(metadataRVA, uint32(len(meta)), entryToken)

	// Assemble .text.
	var text []byte
	text = append(text, cli...)
	for i, body := range bodies {
		for len(text) < int(methodRVAs[i]-textRVA) {
			text = append(text, 0)
		}
		text = append(text, body...)
	}
	for len(text) < metadataOffset {
		text = append(text, 0)
	}
	text = append(text, meta...)

	pe := buildPE(text, textRVA)
	return os.WriteFile(outPath, pe, 0o644)
}
