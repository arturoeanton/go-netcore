package emit

import "github.com/arturoeanton/go-netcore/internal/goir"

// ECMA public key token for the standard reference assemblies (System.*).
var ecmaPublicKeyToken = []byte{0xB0, 0x3F, 0x5F, 0x7F, 0x11, 0xD5, 0x0A, 0x3A}

// goStringCoded / goSliceCoded are the compressed TypeDefOrRef coded indices of
// the GoString (row 6) and GoSlice (row 9) TypeRefs: (row<<2)|1.
const (
	goStringCoded = 0x19 // (6<<2)|1
	goSliceCoded  = 0x25 // (9<<2)|1
	goMapCoded     = 0x2D // (11<<2)|1
	goPtrCoded     = 0x35 // (13<<2)|1
	goClosureCoded = 0x41 // (16<<2)|1
	goChanCoded    = 0x59 // (22<<2)|1
	goInvokerCoded = 0x65 // (25<<2)|1
	goComplexCoded = 0x6D // (27<<2)|1
)

// appendTypeSig appends the signature encoding of a goir.Type. Most types are a
// single ELEMENT_TYPE_* byte; a Go string is the GoString value type and a Go
// struct is a value type in our own assembly, both encoded as VALUETYPE + a
// compressed TypeDefOrRef coded index.
func appendTypeSig(buf []byte, t goir.Type) []byte {
	switch t.Kind {
	case goir.KVoid:
		return append(buf, 0x01) // VOID
	case goir.KInt64:
		return append(buf, 0x0A) // I8
	case goir.KInt32:
		return append(buf, 0x08) // I4
	case goir.KUint64:
		return append(buf, 0x0B) // U8
	case goir.KUint32:
		return append(buf, 0x09) // U4
	case goir.KFloat64:
		return append(buf, 0x0D) // R8
	case goir.KFloat32:
		return append(buf, 0x0C) // R4
	case goir.KBool:
		return append(buf, 0x02) // BOOLEAN
	case goir.KString:
		return append(buf, 0x11, goStringCoded) // VALUETYPE GoString
	case goir.KStruct:
		// VALUETYPE + TypeDefOrRef(tag 0 = TypeDef): (row<<2)|0.
		buf = append(buf, 0x11)
		return appendCompressedUint(buf, uint32(t.Struct.TypeDefRow<<2))
	case goir.KSlice:
		return append(buf, 0x11, goSliceCoded) // VALUETYPE GoSlice
	case goir.KMap:
		return append(buf, 0x12, goMapCoded) // CLASS GoMap (reference type)
	case goir.KPtr:
		return append(buf, 0x12, goPtrCoded) // CLASS GoPtr (reference type)
	case goir.KFunc:
		return append(buf, 0x12, goClosureCoded) // CLASS GoClosure (reference type)
	case goir.KChan:
		return append(buf, 0x12, goChanCoded) // CLASS GoChan (reference type)
	case goir.KComplex:
		return append(buf, 0x12, goComplexCoded) // CLASS GoComplex (reference type)
	case goir.KObjectArray:
		return append(buf, 0x1D, 0x1C) // SZARRAY OBJECT
	case goir.KObject:
		return append(buf, 0x1C) // OBJECT
	default:
		return append(buf, 0x1C)
	}
}

// methodSigBlob builds a DEFAULT (static) method signature.
func methodSigBlob(params []goir.Type, ret goir.Type) []byte {
	sig := []byte{0x00} // calling convention: DEFAULT, no HASTHIS
	sig = appendCompressedUint(sig, uint32(len(params)))
	sig = appendTypeSig(sig, ret)
	for _, p := range params {
		sig = appendTypeSig(sig, p)
	}
	return sig
}

// localVarSigBlob builds a LOCAL_SIG for a method's locals.
func localVarSigBlob(locals []goir.Type) []byte {
	sig := []byte{0x07} // LOCAL_SIG
	sig = appendCompressedUint(sig, uint32(len(locals)))
	for _, l := range locals {
		sig = appendTypeSig(sig, l)
	}
	return sig
}

// Fixed signature blobs for the GoStrings runtime helpers. gs == VALUETYPE
// GoString; s == ELEMENT_TYPE_STRING; i8 == I8; i4 == I4; b == BOOLEAN.
var (
	sigStrFromLit  = []byte{0x00, 0x01, 0x11, goStringCoded, 0x0E}                        // GoString(string)
	sigStrLen      = []byte{0x00, 0x01, 0x0A, 0x11, goStringCoded}                        // i8(gs)
	sigStrByteAt   = []byte{0x00, 0x02, 0x08, 0x11, goStringCoded, 0x0A}                  // i4(gs, i8)
	sigStrConcat   = []byte{0x00, 0x02, 0x11, goStringCoded, 0x11, goStringCoded, 0x11, goStringCoded} // gs(gs,gs)
	sigStrEqual    = []byte{0x00, 0x02, 0x02, 0x11, goStringCoded, 0x11, goStringCoded}   // b(gs,gs)
	sigStrCompare  = []byte{0x00, 0x02, 0x0A, 0x11, goStringCoded, 0x11, goStringCoded}   // i8(gs,gs)
	sigStrRuneAt   = []byte{0x00, 0x02, 0x08, 0x11, goStringCoded, 0x0A}                  // i4(gs, i8)
	sigStrRuneSize = []byte{0x00, 0x02, 0x0A, 0x11, goStringCoded, 0x0A}                  // i8(gs, i8)

	// gs->slice conversions and GoSlices.* (sl == VALUETYPE GoSlice).
	sigStrToSlice  = []byte{0x00, 0x01, 0x11, goSliceCoded, 0x11, goStringCoded}                   // sl(gs)
	sigSliceMake   = []byte{0x00, 0x03, 0x11, goSliceCoded, 0x0A, 0x0A, 0x1C}                      // sl(i8,i8,obj)
	sigSliceGet    = []byte{0x00, 0x02, 0x1C, 0x11, goSliceCoded, 0x0A}                            // obj(sl,i8)
	sigSliceSet    = []byte{0x00, 0x03, 0x01, 0x11, goSliceCoded, 0x0A, 0x1C}                      // void(sl,i8,obj)
	sigSliceLen    = []byte{0x00, 0x01, 0x0A, 0x11, goSliceCoded}                                  // i8(sl)
	sigSliceApp    = []byte{0x00, 0x02, 0x11, goSliceCoded, 0x11, goSliceCoded, 0x1C}              // sl(sl,obj)
	sigSliceSlice  = []byte{0x00, 0x03, 0x11, goSliceCoded, 0x11, goSliceCoded, 0x0A, 0x0A}        // sl(sl,i8,i8)

	// GoMaps.* (mp == CLASS GoMap; obj == OBJECT).
	sigMapMake     = []byte{0x00, 0x00, 0x12, goMapCoded}                                  // mp()
	sigMapLen      = []byte{0x00, 0x01, 0x0A, 0x12, goMapCoded}                            // i8(mp)
	sigMapGet      = []byte{0x00, 0x03, 0x1C, 0x12, goMapCoded, 0x1C, 0x1C}                // obj(mp,obj,obj)
	sigMapContains = []byte{0x00, 0x02, 0x02, 0x12, goMapCoded, 0x1C}                      // b(mp,obj)
	sigMapSet      = []byte{0x00, 0x03, 0x01, 0x12, goMapCoded, 0x1C, 0x1C}                // void(mp,obj,obj)
	sigMapDelete   = []byte{0x00, 0x02, 0x01, 0x12, goMapCoded, 0x1C}                      // void(mp,obj)
	sigMapKeys     = []byte{0x00, 0x01, 0x11, goSliceCoded, 0x12, goMapCoded}              // sl(mp)

	// GoPtrs.* (pt == CLASS GoPtr).
	sigPtrNew = []byte{0x00, 0x02, 0x12, goPtrCoded, 0x1C, 0x0A} // pt(obj, i8 typeId)
	sigPtrGet = []byte{0x00, 0x01, 0x1C, 0x12, goPtrCoded}       // obj(pt)
	sigPtrSet = []byte{0x00, 0x02, 0x01, 0x12, goPtrCoded, 0x1C} // void(pt,obj)

	// GoComplexs.* (cx == CLASS GoComplex; r8 = 0x0D).
	sigCplxMake = []byte{0x00, 0x02, 0x12, goComplexCoded, 0x0D, 0x0D}                       // cx(r8,r8)
	sigCplxBin  = []byte{0x00, 0x02, 0x12, goComplexCoded, 0x12, goComplexCoded, 0x12, goComplexCoded} // cx(cx,cx)
	sigCplxNeg  = []byte{0x00, 0x01, 0x12, goComplexCoded, 0x12, goComplexCoded}             // cx(cx)
	sigCplxEq   = []byte{0x00, 0x02, 0x02, 0x12, goComplexCoded, 0x12, goComplexCoded}       // bool(cx,cx)
	sigCplxPart = []byte{0x00, 0x01, 0x0D, 0x12, goComplexCoded}                             // r8(cx)

	// GoChans.* (gc == CLASS GoChan).
	sigChanMake  = []byte{0x00, 0x01, 0x12, goChanCoded, 0x0A}             // gc(i8)
	sigChanSend  = []byte{0x00, 0x02, 0x01, 0x12, goChanCoded, 0x1C}       // void(gc,obj)
	sigChanRecv  = []byte{0x00, 0x01, 0x1C, 0x12, goChanCoded}             // obj(gc)
	sigChanRecv2 = []byte{0x00, 0x01, 0x1D, 0x1C, 0x12, goChanCoded}       // obj[](gc)
	sigChanClose = []byte{0x00, 0x01, 0x01, 0x12, goChanCoded}             // void(gc)
	sigChanLen   = []byte{0x00, 0x01, 0x0A, 0x12, goChanCoded}             // i8(gc)
)

// buildTables serializes the #~ stream for a whole program. methodRVAs is
// parallel to prog.Methods; sigBlobOffsets holds the #Blob offset of each
// StandAloneSig row (one per method that declares locals, in method order).
func buildTables(prog *goir.Program, h *heaps, methodRVAs []uint32, sigBlobOffsets []uint16, ext *externCollection) []byte {
	moduleName := prog.AssemblyName + ".dll"

	// Strings.
	sModule := h.addString(moduleName)
	mvidIdx := h.addGUID(mvid)
	sObject := h.addString("Object")
	sInt64 := h.addString("Int64")
	sBoolean := h.addString("Boolean")
	sInt32 := h.addString("Int32")
	sSystem := h.addString("System")
	sBuiltins := h.addString("Builtins")
	sGoString := h.addString("GoString")
	sGoStrings := h.addString("GoStrings")
	sGoSlice := h.addString("GoSlice")
	sGoSlices := h.addString("GoSlices")
	sGoMap := h.addString("GoMap")
	sGoMaps := h.addString("GoMaps")
	sGoPtr := h.addString("GoPtr")
	sGoPtrs := h.addString("GoPtrs")
	sGoPanic := h.addString("GoPanicException")
	sGoClosure := h.addString("GoClosure")
	sGoClosures := h.addString("GoClosures")
	sDouble := h.addString("Double")
	sSingle := h.addString("Single")
	sUInt64 := h.addString("UInt64")
	sUInt32 := h.addString("UInt32")
	sGoChan := h.addString("GoChan")
	sGoChans := h.addString("GoChans")
	sGoRuntime := h.addString("GoRuntime")
	sGoInvoker := h.addString("GoInvoker")
	sGoSelect := h.addString("GoSelect")
	sGoComplex := h.addString("GoComplex")
	sGoComplexs := h.addString("GoComplexs")
	sGoDefers := h.addString("GoDefers")
	sRuntimeNS := h.addString(runtimeNamespace)
	sModuleType := h.addString("<Module>")
	sProgram := h.addString("Program")
	sEmpty := h.addString("")
	sAsm := h.addString(prog.AssemblyName)
	sSysRuntime := h.addString("System.Runtime")
	sGoclrRuntime := h.addString(runtimeAssembly)
	sValueType := h.addString("ValueType")

	// Field rows: package-level globals first (static fields on Program), then
	// each struct's instance fields. structFieldStart[i] is the 1-based Field-table
	// row of struct i's first field (its TypeDef.FieldList).
	type fieldRow struct {
		name, sig uint16
		static    bool
	}
	var fieldRows []fieldRow
	for _, g := range prog.Globals {
		fieldSig := append([]byte{0x06}, appendTypeSig(nil, g.Type)...) // FIELD sig
		fieldRows = append(fieldRows, fieldRow{
			name:   h.addString(g.Name),
			sig:    h.addBlob(fieldSig),
			static: true,
		})
	}
	structNameIdx := make([]uint16, len(prog.Structs))
	structFieldStart := make([]int, len(prog.Structs))
	for i, s := range prog.Structs {
		structNameIdx[i] = h.addString(s.Name)
		structFieldStart[i] = len(fieldRows) + 1
		for _, f := range s.Fields {
			fieldSig := append([]byte{0x06}, appendTypeSig(nil, f.Type)...) // FIELD sig
			fieldRows = append(fieldRows, fieldRow{
				name: h.addString(f.Name),
				sig:  h.addBlob(fieldSig),
			})
		}
	}
	hasFields := len(fieldRows) > 0

	// Method names and signatures.
	methodNameIdx := make([]uint16, len(prog.Methods))
	methodSigIdx := make([]uint16, len(prog.Methods))
	for i, m := range prog.Methods {
		methodNameIdx[i] = h.addString(methodCLRName(m))
		methodSigIdx[i] = h.addBlob(methodSigBlob(m.Params, m.Ret))
	}

	bPrintln := h.addBlob([]byte{0x00, 0x01, 0x01, 0x1D, 0x1C}) // void(object[])
	bEcmaToken := h.addBlob(ecmaPublicKeyToken)

	// GoStrings member names and signature blobs.
	type member struct {
		name string
		blob []byte
	}
	strMembers := []member{
		{"FromLiteral", sigStrFromLit},
		{"Len", sigStrLen},
		{"ByteAt", sigStrByteAt},
		{"Concat", sigStrConcat},
		{"Equal", sigStrEqual},
		{"Compare", sigStrCompare},
		{"RuneAt", sigStrRuneAt},
		{"RuneSize", sigStrRuneSize},
		{"ToByteSlice", sigStrToSlice},
		{"ToRuneSlice", sigStrToSlice},
	}
	strMemberName := make([]uint16, len(strMembers))
	strMemberSig := make([]uint16, len(strMembers))
	for i, m := range strMembers {
		strMemberName[i] = h.addString(m.name)
		strMemberSig[i] = h.addBlob(m.blob)
	}

	// GoSlices member names and signatures (MemberRef parent = TypeRef[10]).
	sliceMembers := []member{
		{"Make", sigSliceMake},
		{"Get", sigSliceGet},
		{"Set", sigSliceSet},
		{"Len", sigSliceLen},
		{"Cap", sigSliceLen}, // same signature as Len
		{"AppendOne", sigSliceApp},
		{"Slice", sigSliceSlice},
	}
	sliceMemberName := make([]uint16, len(sliceMembers))
	sliceMemberSig := make([]uint16, len(sliceMembers))
	for i, m := range sliceMembers {
		sliceMemberName[i] = h.addString(m.name)
		sliceMemberSig[i] = h.addBlob(m.blob)
	}

	// GoMaps member names and signatures (MemberRef parent = TypeRef[12]).
	mapMembers := []member{
		{"Make", sigMapMake},
		{"Len", sigMapLen},
		{"Get", sigMapGet},
		{"Contains", sigMapContains},
		{"Set", sigMapSet},
		{"Delete", sigMapDelete},
		{"Keys", sigMapKeys},
	}
	mapMemberName := make([]uint16, len(mapMembers))
	mapMemberSig := make([]uint16, len(mapMembers))
	for i, m := range mapMembers {
		mapMemberName[i] = h.addString(m.name)
		mapMemberSig[i] = h.addBlob(m.blob)
	}

	// GoPtrs member names and signatures (MemberRef parent = TypeRef[14]).
	ptrMembers := []member{
		{"New", sigPtrNew},
		{"Get", sigPtrGet},
		{"Set", sigPtrSet},
	}
	ptrMemberName := make([]uint16, len(ptrMembers))
	ptrMemberSig := make([]uint16, len(ptrMembers))
	for i, m := range ptrMembers {
		ptrMemberName[i] = h.addString(m.name)
		ptrMemberSig[i] = h.addBlob(m.blob)
	}

	// GoChans member names and signatures (MemberRef parent = TypeRef[23]).
	chanMembers := []member{
		{"Make", sigChanMake},
		{"Send", sigChanSend},
		{"Recv", sigChanRecv},
		{"Recv2", sigChanRecv2},
		{"Close", sigChanClose},
		{"Len", sigChanLen},
		{"Cap", sigChanLen}, // same signature as Len
	}
	chanMemberName := make([]uint16, len(chanMembers))
	chanMemberSig := make([]uint16, len(chanMembers))
	for i, m := range chanMembers {
		chanMemberName[i] = h.addString(m.name)
		chanMemberSig[i] = h.addBlob(m.blob)
	}

	// Coded indices.
	scopeSysRuntime := uint16(1<<2 | 2)   // ResolutionScope -> AssemblyRef[1]
	scopeGoclrRuntime := uint16(2<<2 | 2) // ResolutionScope -> AssemblyRef[2]
	extendsObject := uint16(1<<2 | 1)     // TypeDefOrRef -> TypeRef[1]
	parentBuiltins := uint16(5<<3 | 1)    // MemberRefParent -> TypeRef[5]
	parentGoStrings := uint16(7<<3 | 1)   // MemberRefParent -> TypeRef[7]
	parentGoSlices := uint16(10<<3 | 1)   // MemberRefParent -> TypeRef[10]
	parentGoMaps := uint16(12<<3 | 1)     // MemberRefParent -> TypeRef[12]
	parentGoPtrs := uint16(14<<3 | 1)     // MemberRefParent -> TypeRef[14]

	hasLocals := len(sigBlobOffsets) > 0

	var out []byte
	w := &writer{&out}

	// --- #~ stream header ---
	w.u32(0)
	w.u8(2) // major
	w.u8(0) // minor
	w.u8(0) // heap sizes (2-byte)
	w.u8(1) // reserved
	valid := uint64(1)<<0 | 1<<1 | 1<<2 | 1<<6 | 1<<10 | 1<<32 | 1<<35
	if hasFields {
		valid |= 1 << 4 // Field
	}
	if hasLocals {
		valid |= 1 << 17 // StandAloneSig
	}
	w.u64(valid)
	w.u64(0) // sorted

	// Pre-add the heap entries for the dynamic shim rows so their offsets are set.
	type extTypeRow struct{ scope, name, ns uint16 }
	extTypes := make([]extTypeRow, len(ext.types))
	for i, et := range ext.types {
		extTypes[i] = extTypeRow{
			scope: uint16(et.asmRow<<2 | 2), // ResolutionScope -> AssemblyRef[asmRow]
			name:  h.addString(et.name),
			ns:    h.addString(et.namespace),
		}
	}
	type extMemberRow struct{ parent, name, sig uint16 }
	extMembers := make([]extMemberRow, len(ext.methods))
	for i, em := range ext.methods {
		extMembers[i] = extMemberRow{
			parent: uint16(ext.typeRowOf[em.TypeKey()]<<3 | 1), // MemberRefParent -> TypeRef
			name:   h.addString(em.Method),
			sig:    h.addBlob(methodSigBlob(em.Params, em.Ret)),
		}
	}
	extAsmName := make([]uint16, len(ext.assemblies))
	for i, a := range ext.assemblies {
		extAsmName[i] = h.addString(a)
	}

	// Row counts, ascending table id.
	w.u32(1)                                            // Module
	w.u32(uint32(29 + len(ext.types)))                  // TypeRef (fixed + shim types)
	w.u32(uint32(2 + len(prog.Structs)))                // TypeDef (<Module>, Program, structs)
	if hasFields {
		w.u32(uint32(len(fieldRows))) // Field
	}
	w.u32(uint32(len(prog.Methods))) // MethodDef
	// MemberRef: 60 fixed (Println/Print + runtime helpers) + shim methods.
	w.u32(uint32(2 + len(strMembers) + len(sliceMembers) + len(mapMembers) + len(ptrMembers) + 4 + 3 + len(chanMembers) + 3 + 1 + 1 + 9 + 3 + len(ext.methods)))
	if hasLocals {
		w.u32(uint32(len(sigBlobOffsets))) // StandAloneSig
	}
	w.u32(1)                              // Assembly
	w.u32(uint32(2 + len(ext.assemblies))) // AssemblyRef (System.Runtime, GoCLR.Runtime, shims)

	// --- Module (0x00) ---
	w.u16(0)
	w.u16(sModule)
	w.u16(mvidIdx)
	w.u16(0)
	w.u16(0)

	// --- TypeRef (0x01) ---
	typeRef := func(scope, name, ns uint16) { w.u16(scope); w.u16(name); w.u16(ns) }
	typeRef(scopeSysRuntime, sObject, sSystem)        // [1]
	typeRef(scopeSysRuntime, sInt64, sSystem)         // [2]
	typeRef(scopeSysRuntime, sBoolean, sSystem)       // [3]
	typeRef(scopeSysRuntime, sInt32, sSystem)         // [4]
	typeRef(scopeGoclrRuntime, sBuiltins, sRuntimeNS) // [5]
	typeRef(scopeGoclrRuntime, sGoString, sRuntimeNS)  // [6]
	typeRef(scopeGoclrRuntime, sGoStrings, sRuntimeNS) // [7]
	typeRef(scopeSysRuntime, sValueType, sSystem)      // [8] System.ValueType
	typeRef(scopeGoclrRuntime, sGoSlice, sRuntimeNS)   // [9] GoSlice
	typeRef(scopeGoclrRuntime, sGoSlices, sRuntimeNS)  // [10] GoSlices
	typeRef(scopeGoclrRuntime, sGoMap, sRuntimeNS)     // [11] GoMap
	typeRef(scopeGoclrRuntime, sGoMaps, sRuntimeNS)    // [12] GoMaps
	typeRef(scopeGoclrRuntime, sGoPtr, sRuntimeNS)     // [13] GoPtr
	typeRef(scopeGoclrRuntime, sGoPtrs, sRuntimeNS)    // [14] GoPtrs
	typeRef(scopeGoclrRuntime, sGoPanic, sRuntimeNS)     // [15] GoPanicException
	typeRef(scopeGoclrRuntime, sGoClosure, sRuntimeNS)   // [16] GoClosure
	typeRef(scopeGoclrRuntime, sGoClosures, sRuntimeNS)  // [17] GoClosures
	typeRef(scopeSysRuntime, sDouble, sSystem)           // [18] System.Double
	typeRef(scopeSysRuntime, sSingle, sSystem)           // [19] System.Single
	typeRef(scopeSysRuntime, sUInt64, sSystem)           // [20] System.UInt64
	typeRef(scopeSysRuntime, sUInt32, sSystem)           // [21] System.UInt32
	typeRef(scopeGoclrRuntime, sGoChan, sRuntimeNS)      // [22] GoChan
	typeRef(scopeGoclrRuntime, sGoChans, sRuntimeNS)     // [23] GoChans
	typeRef(scopeGoclrRuntime, sGoRuntime, sRuntimeNS)   // [24] GoRuntime
	typeRef(scopeGoclrRuntime, sGoInvoker, sRuntimeNS)   // [25] GoInvoker
	typeRef(scopeGoclrRuntime, sGoSelect, sRuntimeNS)    // [26] GoSelect
	typeRef(scopeGoclrRuntime, sGoComplex, sRuntimeNS)   // [27] GoComplex
	typeRef(scopeGoclrRuntime, sGoComplexs, sRuntimeNS)  // [28] GoComplexs
	typeRef(scopeGoclrRuntime, sGoDefers, sRuntimeNS)    // [29] GoDefers
	for _, et := range extTypes {                        // [30+] shim types
		typeRef(et.scope, et.name, et.ns)
	}

	// --- TypeDef (0x02) ---
	// [1] <Module>.
	w.u32(0)
	w.u16(sModuleType)
	w.u16(sEmpty)
	w.u16(0) // extends null
	w.u16(1) // FieldList
	w.u16(1) // MethodList (no methods)
	// [2] Program : System.Object — owns all methods.
	w.u32(0x00100001) // Public | BeforeFieldInit
	w.u16(sProgram)
	w.u16(sEmpty)
	w.u16(extendsObject)
	w.u16(1) // FieldList (owns none; structs' fields start at 1)
	w.u16(1) // MethodList -> MethodDef[1] (owns all methods)
	// [3..] struct value types, extending System.ValueType.
	extendsValueType := uint16(8<<2 | 1)              // TypeDefOrRef -> TypeRef[8]
	structMethodList := uint16(len(prog.Methods) + 1) // structs own no methods
	for i := range prog.Structs {
		w.u32(0x00100109) // Public | SequentialLayout | Sealed | BeforeFieldInit
		w.u16(structNameIdx[i])
		w.u16(sEmpty)
		w.u16(extendsValueType)
		w.u16(uint16(structFieldStart[i])) // FieldList
		w.u16(structMethodList)            // MethodList
	}

	// --- Field (0x04) ---
	for _, fr := range fieldRows {
		flags := uint16(0x0006) // Public
		if fr.static {
			flags = 0x0016 // Public | Static
		}
		w.u16(flags)
		w.u16(fr.name)
		w.u16(fr.sig)
	}

	// --- MethodDef (0x06) ---
	for i := range prog.Methods {
		w.u32(methodRVAs[i])     // RVA
		w.u16(0)                 // ImplFlags
		w.u16(0x0016)            // Public | Static
		w.u16(methodNameIdx[i])  // Name
		w.u16(methodSigIdx[i])   // Signature
		w.u16(1)                 // ParamList (Param table empty)
	}

	// --- MemberRef (0x0A) ---
	sPrintlnName := h.addString("Println")
	sPrintName := h.addString("Print")
	w.u16(parentBuiltins) // [1] Println
	w.u16(sPrintlnName)
	w.u16(bPrintln)
	w.u16(parentBuiltins) // [2] Print
	w.u16(sPrintName)
	w.u16(bPrintln)
	for i := range strMembers { // GoStrings.* (incl. To{Byte,Rune}Slice)
		w.u16(parentGoStrings)
		w.u16(strMemberName[i])
		w.u16(strMemberSig[i])
	}
	for i := range sliceMembers { // GoSlices.*
		w.u16(parentGoSlices)
		w.u16(sliceMemberName[i])
		w.u16(sliceMemberSig[i])
	}
	for i := range mapMembers { // GoMaps.*
		w.u16(parentGoMaps)
		w.u16(mapMemberName[i])
		w.u16(mapMemberSig[i])
	}
	for i := range ptrMembers { // GoPtrs.*
		w.u16(parentGoPtrs)
		w.u16(ptrMemberName[i])
		w.u16(ptrMemberSig[i])
	}
	// [30..33] Builtins.Panic / Recover / SetPanic / PanicHandled.
	w.u16(parentBuiltins)
	w.u16(h.addString("Panic"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x01, 0x1C})) // void(object)
	w.u16(parentBuiltins)
	w.u16(h.addString("Recover"))
	w.u16(h.addBlob([]byte{0x00, 0x00, 0x1C})) // object()
	w.u16(parentBuiltins)
	w.u16(h.addString("SetPanic"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, 0x3D})) // void(class GoPanicException)
	w.u16(parentBuiltins)
	w.u16(h.addString("PanicHandled"))
	w.u16(h.addBlob([]byte{0x00, 0x00, 0x02})) // bool()
	// [34..36] GoClosures.New / Id / Env.
	parentGoClosures := uint16(17<<3 | 1)
	w.u16(parentGoClosures)
	w.u16(h.addString("New"))
	w.u16(h.addBlob([]byte{0x00, 0x02, 0x12, goClosureCoded, 0x0A, 0x1D, 0x1C})) // GoClosure(i8, object[])
	w.u16(parentGoClosures)
	w.u16(h.addString("Id"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x0A, 0x12, goClosureCoded})) // i8(GoClosure)
	w.u16(parentGoClosures)
	w.u16(h.addString("Env"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x1D, 0x1C, 0x12, goClosureCoded})) // object[](GoClosure)
	// [37..43] GoChans.* (Make/Send/Recv/Recv2/Close/Len/Cap).
	parentGoChans := uint16(23<<3 | 1)
	for i := range chanMembers {
		w.u16(parentGoChans)
		w.u16(chanMemberName[i])
		w.u16(chanMemberSig[i])
	}
	// [44] GoRuntime.Go(GoClosure).
	parentGoRuntime := uint16(24<<3 | 1)
	w.u16(parentGoRuntime)
	w.u16(h.addString("Go"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, goClosureCoded})) // void(GoClosure)
	// [45] GoRuntime.SetInvoker(GoInvoker).
	w.u16(parentGoRuntime)
	w.u16(h.addString("SetInvoker"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, goInvokerCoded})) // void(GoInvoker)
	// [46] GoInvoker::.ctor(object, native int) — delegate constructor.
	parentGoInvoker := uint16(25<<3 | 1)
	w.u16(parentGoInvoker)
	w.u16(h.addString(".ctor"))
	w.u16(h.addBlob([]byte{0x20, 0x02, 0x01, 0x1C, 0x18})) // HASTHIS void(object, native int)
	// [47] GoSelect.Select(object[], object[], object[], bool) -> object[].
	parentGoSelect := uint16(26<<3 | 1)
	w.u16(parentGoSelect)
	w.u16(h.addString("Select"))
	w.u16(h.addBlob([]byte{0x00, 0x04, 0x1D, 0x1C, 0x1D, 0x1C, 0x1D, 0x1C, 0x1D, 0x1C, 0x02}))
	// [48] GoPtrs.TypeIdOf(GoPtr) -> i8 (appended last to keep earlier tokens stable).
	w.u16(parentGoPtrs)
	w.u16(h.addString("TypeIdOf"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x0A, 0x12, goPtrCoded})) // i8(pt)
	// [49..57] GoComplexs.* (Make/Add/Sub/Mul/Div/Neg/Eq/Real/Imag).
	parentGoComplexs := uint16(28<<3 | 1)
	cplx := func(name string, blob []byte) {
		w.u16(parentGoComplexs)
		w.u16(h.addString(name))
		w.u16(h.addBlob(blob))
	}
	cplx("Make", sigCplxMake)
	cplx("Add", sigCplxBin)
	cplx("Sub", sigCplxBin)
	cplx("Mul", sigCplxBin)
	cplx("Div", sigCplxBin)
	cplx("Neg", sigCplxNeg)
	cplx("Eq", sigCplxEq)
	cplx("Real", sigCplxPart)
	cplx("Imag", sigCplxPart)
	// [58..60] GoDefers.Mark() -> i8, Push(GoClosure), Run(i8).
	parentGoDefers := uint16(29<<3 | 1)
	w.u16(parentGoDefers)
	w.u16(h.addString("Mark"))
	w.u16(h.addBlob([]byte{0x00, 0x00, 0x0A})) // i8()
	w.u16(parentGoDefers)
	w.u16(h.addString("Push"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, goClosureCoded})) // void(GoClosure)
	w.u16(parentGoDefers)
	w.u16(h.addString("Run"))
	w.u16(h.addBlob([]byte{0x00, 0x01, 0x01, 0x0A})) // void(i8)
	for _, em := range extMembers { // [61+] shim methods
		w.u16(em.parent)
		w.u16(em.name)
		w.u16(em.sig)
	}

	// --- StandAloneSig (0x11) ---
	if hasLocals {
		for _, off := range sigBlobOffsets {
			w.u16(off)
		}
	}

	// --- Assembly (0x20) ---
	w.u32(0x8004) // SHA1
	w.u16(1)      // Major
	w.u16(0)
	w.u16(0)
	w.u16(0)
	w.u32(0) // Flags
	w.u16(0) // PublicKey
	w.u16(sAsm)
	w.u16(0) // Culture

	// --- AssemblyRef (0x23) ---
	// [1] System.Runtime 8.0.0.0.
	w.u16(8)
	w.u16(0)
	w.u16(0)
	w.u16(0)
	w.u32(0)
	w.u16(bEcmaToken)
	w.u16(sSysRuntime)
	w.u16(0)
	w.u16(0)
	// [2] GoCLR.Runtime 1.0.0.0.
	w.u16(1)
	w.u16(0)
	w.u16(0)
	w.u16(0)
	w.u32(0)
	w.u16(0)
	w.u16(sGoclrRuntime)
	w.u16(0)
	w.u16(0)
	// [3+] shim assemblies (GoCLR.Stdlib, ...) 1.0.0.0.
	for _, name := range extAsmName {
		w.u16(1)
		w.u16(0)
		w.u16(0)
		w.u16(0)
		w.u32(0)
		w.u16(0)
		w.u16(name)
		w.u16(0)
		w.u16(0)
	}

	return out
}

// methodCLRName maps a Go function name to its emitted method name. main becomes
// "Main" (the entry point); others keep their Go name.
func methodCLRName(m *goir.Method) string {
	if m.GoName == "main" {
		return "Main"
	}
	return m.Name
}
