package emit

import "github.com/arturoeanton/go-netcore/internal/goir"

// ECMA public key token for the standard reference assemblies (System.*).
var ecmaPublicKeyToken = []byte{0xB0, 0x3F, 0x5F, 0x7F, 0x11, 0xD5, 0x0A, 0x3A}

// goStringCoded / goSliceCoded are the compressed TypeDefOrRef coded indices of
// the GoString (row 6) and GoSlice (row 9) TypeRefs: (row<<2)|1.
const (
	goStringCoded  = 0x19 // (6<<2)|1
	goSliceCoded   = 0x25 // (9<<2)|1
	goMapCoded     = 0x2D // (11<<2)|1
	goPtrCoded     = 0x35 // (13<<2)|1
	goClosureCoded = 0x41 // (16<<2)|1
	goChanCoded    = 0x59 // (22<<2)|1
	goInvokerCoded = 0x65 // (25<<2)|1
	goComplexCoded = 0x6D // (27<<2)|1
	goErrorCoded   = 0x79 // (30<<2)|1
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
	sigStrFromLit  = []byte{0x00, 0x01, 0x11, goStringCoded, 0x0E}                                     // GoString(string)
	sigStrLen      = []byte{0x00, 0x01, 0x0A, 0x11, goStringCoded}                                     // i8(gs)
	sigStrByteAt   = []byte{0x00, 0x02, 0x08, 0x11, goStringCoded, 0x0A}                               // i4(gs, i8)
	sigStrConcat   = []byte{0x00, 0x02, 0x11, goStringCoded, 0x11, goStringCoded, 0x11, goStringCoded} // gs(gs,gs)
	sigStrEqual    = []byte{0x00, 0x02, 0x02, 0x11, goStringCoded, 0x11, goStringCoded}                // b(gs,gs)
	sigStrCompare  = []byte{0x00, 0x02, 0x0A, 0x11, goStringCoded, 0x11, goStringCoded}                // i8(gs,gs)
	sigStrRuneAt   = []byte{0x00, 0x02, 0x08, 0x11, goStringCoded, 0x0A}                               // i4(gs, i8)
	sigStrRuneSize = []byte{0x00, 0x02, 0x0A, 0x11, goStringCoded, 0x0A}                               // i8(gs, i8)

	// gs->slice conversions and GoSlices.* (sl == VALUETYPE GoSlice).
	sigStrToSlice = []byte{0x00, 0x01, 0x11, goSliceCoded, 0x11, goStringCoded}            // sl(gs)
	sigSliceMake  = []byte{0x00, 0x03, 0x11, goSliceCoded, 0x0A, 0x0A, 0x1C}               // sl(i8,i8,obj)
	sigSliceGet   = []byte{0x00, 0x02, 0x1C, 0x11, goSliceCoded, 0x0A}                     // obj(sl,i8)
	sigSliceSet   = []byte{0x00, 0x03, 0x01, 0x11, goSliceCoded, 0x0A, 0x1C}               // void(sl,i8,obj)
	sigSliceLen   = []byte{0x00, 0x01, 0x0A, 0x11, goSliceCoded}                           // i8(sl)
	sigSliceApp   = []byte{0x00, 0x02, 0x11, goSliceCoded, 0x11, goSliceCoded, 0x1C}       // sl(sl,obj)
	sigSliceSlice = []byte{0x00, 0x03, 0x11, goSliceCoded, 0x11, goSliceCoded, 0x0A, 0x0A} // sl(sl,i8,i8)

	// GoMaps.* (mp == CLASS GoMap; obj == OBJECT).
	sigMapMake     = []byte{0x00, 0x00, 0x12, goMapCoded}                     // mp()
	sigMapLen      = []byte{0x00, 0x01, 0x0A, 0x12, goMapCoded}               // i8(mp)
	sigMapGet      = []byte{0x00, 0x03, 0x1C, 0x12, goMapCoded, 0x1C, 0x1C}   // obj(mp,obj,obj)
	sigMapContains = []byte{0x00, 0x02, 0x02, 0x12, goMapCoded, 0x1C}         // b(mp,obj)
	sigMapSet      = []byte{0x00, 0x03, 0x01, 0x12, goMapCoded, 0x1C, 0x1C}   // void(mp,obj,obj)
	sigMapDelete   = []byte{0x00, 0x02, 0x01, 0x12, goMapCoded, 0x1C}         // void(mp,obj)
	sigMapKeys     = []byte{0x00, 0x01, 0x11, goSliceCoded, 0x12, goMapCoded} // sl(mp)

	// GoPtrs.* (pt == CLASS GoPtr).
	sigPtrNew = []byte{0x00, 0x02, 0x12, goPtrCoded, 0x1C, 0x0A} // pt(obj, i8 typeId)
	sigPtrGet = []byte{0x00, 0x01, 0x1C, 0x12, goPtrCoded}       // obj(pt)
	sigPtrSet = []byte{0x00, 0x02, 0x01, 0x12, goPtrCoded, 0x1C} // void(pt,obj)

	// GoComplexs.* (cx == CLASS GoComplex; r8 = 0x0D).
	sigCplxMake = []byte{0x00, 0x02, 0x12, goComplexCoded, 0x0D, 0x0D}                                 // cx(r8,r8)
	sigCplxBin  = []byte{0x00, 0x02, 0x12, goComplexCoded, 0x12, goComplexCoded, 0x12, goComplexCoded} // cx(cx,cx)
	sigCplxNeg  = []byte{0x00, 0x01, 0x12, goComplexCoded, 0x12, goComplexCoded}                       // cx(cx)
	sigCplxEq   = []byte{0x00, 0x02, 0x02, 0x12, goComplexCoded, 0x12, goComplexCoded}                 // bool(cx,cx)
	sigCplxPart = []byte{0x00, 0x01, 0x0D, 0x12, goComplexCoded}                                       // r8(cx)

	// GoChans.* (gc == CLASS GoChan).
	sigChanMake  = []byte{0x00, 0x01, 0x12, goChanCoded, 0x0A}       // gc(i8)
	sigChanSend  = []byte{0x00, 0x02, 0x01, 0x12, goChanCoded, 0x1C} // void(gc,obj)
	sigChanRecv  = []byte{0x00, 0x01, 0x1C, 0x12, goChanCoded}       // obj(gc)
	sigChanRecv2 = []byte{0x00, 0x01, 0x1D, 0x1C, 0x12, goChanCoded} // obj[](gc)
	sigChanClose = []byte{0x00, 0x01, 0x01, 0x12, goChanCoded}       // void(gc)
	sigChanLen   = []byte{0x00, 0x01, 0x0A, 0x12, goChanCoded}       // i8(gc)
)

// buildTables serializes the #~ stream for a whole program. methodRVAs is
// parallel to prog.Methods; sigBlobOffsets holds the #Blob offset of each
// StandAloneSig row (one per method that declares locals, in method order).
func buildTables(prog *goir.Program, h *heaps, methodRVAs []uint32, sigBlobOffsets []uint32, ext *externCollection) []byte {
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
	sGoError := h.addString("IGoError")
	sGoErrors := h.addString("GoErrors")
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
		name, sig uint32 // #Strings, #Blob offsets
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
	structNameIdx := make([]uint32, len(prog.Structs))
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
	methodNameIdx := make([]uint32, len(prog.Methods))
	methodSigIdx := make([]uint32, len(prog.Methods))
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
	strMemberName := make([]uint32, len(strMembers))
	strMemberSig := make([]uint32, len(strMembers))
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
	sliceMemberName := make([]uint32, len(sliceMembers))
	sliceMemberSig := make([]uint32, len(sliceMembers))
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
	mapMemberName := make([]uint32, len(mapMembers))
	mapMemberSig := make([]uint32, len(mapMembers))
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
	ptrMemberName := make([]uint32, len(ptrMembers))
	ptrMemberSig := make([]uint32, len(ptrMembers))
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
	chanMemberName := make([]uint32, len(chanMembers))
	chanMemberSig := make([]uint32, len(chanMembers))
	for i, m := range chanMembers {
		chanMemberName[i] = h.addString(m.name)
		chanMemberSig[i] = h.addBlob(m.blob)
	}

	// Coded indices.
	scopeSysRuntime := uint32(1<<2 | 2)   // ResolutionScope -> AssemblyRef[1]
	scopeGoclrRuntime := uint32(2<<2 | 2) // ResolutionScope -> AssemblyRef[2]
	extendsObject := uint32(1<<2 | 1)     // TypeDefOrRef -> TypeRef[1]
	parentBuiltins := uint32(5<<3 | 1)    // MemberRefParent -> TypeRef[5]
	parentGoStrings := uint32(7<<3 | 1)   // MemberRefParent -> TypeRef[7]
	parentGoSlices := uint32(10<<3 | 1)   // MemberRefParent -> TypeRef[10]
	parentGoMaps := uint32(12<<3 | 1)     // MemberRefParent -> TypeRef[12]
	parentGoPtrs := uint32(14<<3 | 1)     // MemberRefParent -> TypeRef[14]

	// Index widths (ECMA-335 II.24.2.6): a coded index is 4 bytes when the largest
	// referenced table has >= 2^(16-tagBits) rows; a simple index is 4 bytes when its
	// target table has >= 65536 rows. goclr previously hard-coded 2 bytes, which corrupted
	// the metadata of large programs (e.g. fiber+goja: MethodDef >= 8192 forces the 3-bit
	// MemberRefParent coded index to 4 bytes). Row counts are known here, before any row
	// is written, so the widths are fixed for the whole tables stream.
	nTypeRef := fixedTypeRefs + len(ext.types)
	nTypeDef := 2 + len(prog.Structs)
	nField := len(fieldRows)
	nMethodDef := len(prog.Methods)
	nAssemblyRef := 2 + len(ext.assemblies)
	maxRows := func(a ...int) int {
		m := 0
		for _, x := range a {
			if x > m {
				m = x
			}
		}
		return m
	}
	codedWide := func(maxR, tagBits int) bool { return maxR >= (1 << (16 - tagBits)) }
	simpleWide := func(rows int) bool { return rows >= 65536 }
	// ResolutionScope (2 bits): Module, ModuleRef(0), AssemblyRef, TypeRef.
	wResScope := codedWide(maxRows(1, 0, nAssemblyRef, nTypeRef), 2)
	// TypeDefOrRef (2 bits): TypeDef, TypeRef, TypeSpec(0).
	wTypeDefOrRef := codedWide(maxRows(nTypeDef, nTypeRef, 0), 2)
	// MemberRefParent (3 bits): TypeDef, TypeRef, ModuleRef(0), MethodDef, TypeSpec(0).
	wMemberRefParent := codedWide(maxRows(nTypeDef, nTypeRef, 0, nMethodDef, 0), 3)
	wFieldIdx := simpleWide(nField)     // FieldList -> Field
	wMethodIdx := simpleWide(nMethodDef) // MethodList -> MethodDef
	wParamIdx := simpleWide(0)           // ParamList -> Param (empty)

	hasLocals := len(sigBlobOffsets) > 0

	var out []byte
	w := &writer{&out}

	// --- #~ stream header ---
	w.u32(0)
	w.u8(2) // major
	w.u8(0) // minor
	w.u8(7) // heap sizes: #Strings/#GUID/#Blob all 4-byte (large heaps)
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
	type extTypeRow struct {
		scope    uint32 // ResolutionScope coded index
		name, ns uint32 // #Strings offsets
	}
	extTypes := make([]extTypeRow, len(ext.types))
	for i, et := range ext.types {
		extTypes[i] = extTypeRow{
			scope: uint32(et.asmRow<<2 | 2), // ResolutionScope -> AssemblyRef[asmRow]
			name:  h.addString(et.name),
			ns:    h.addString(et.namespace),
		}
	}
	type extMemberRow struct {
		parent    uint32 // MemberRefParent coded index
		name, sig uint32 // #Strings, #Blob offsets
	}
	extMembers := make([]extMemberRow, len(ext.methods))
	for i, em := range ext.methods {
		extMembers[i] = extMemberRow{
			parent: uint32(ext.typeRowOf[em.TypeKey()]<<3 | 1), // MemberRefParent -> TypeRef
			name:   h.addString(em.Method),
			sig:    h.addBlob(methodSigBlob(em.Params, em.Ret)),
		}
	}
	extAsmName := make([]uint32, len(ext.assemblies))
	for i, a := range ext.assemblies {
		extAsmName[i] = h.addString(a)
	}

	// Row counts, ascending table id.
	w.u32(1)                                      // Module
	w.u32(uint32(fixedTypeRefs + len(ext.types))) // TypeRef (fixed + shim types)
	w.u32(uint32(2 + len(prog.Structs)))          // TypeDef (<Module>, Program, structs)
	if hasFields {
		w.u32(uint32(len(fieldRows))) // Field
	}
	w.u32(uint32(len(prog.Methods))) // MethodDef
	// MemberRef: fixed (Println/Print + runtime helpers + GoErrors.Error) + shim methods.
	w.u32(uint32(fixedMemberRefs + len(ext.methods)))
	if hasLocals {
		w.u32(uint32(len(sigBlobOffsets))) // StandAloneSig
	}
	w.u32(1)                               // Assembly
	w.u32(uint32(2 + len(ext.assemblies))) // AssemblyRef (System.Runtime, GoCLR.Runtime, shims)

	// --- Module (0x00) ---
	w.u16(0)        // Generation
	w.heap(sModule) // Name (#Strings)
	w.heap(mvidIdx) // Mvid (#GUID)
	w.heap(0)       // EncId (#GUID)
	w.heap(0)       // EncBaseId (#GUID)

	// --- TypeRef (0x01) ---
	typeRef := func(scope uint32, name, ns uint32) { w.idx(scope, wResScope); w.heap(name); w.heap(ns) }
	typeRef(scopeSysRuntime, sObject, sSystem)          // [1]
	typeRef(scopeSysRuntime, sInt64, sSystem)           // [2]
	typeRef(scopeSysRuntime, sBoolean, sSystem)         // [3]
	typeRef(scopeSysRuntime, sInt32, sSystem)           // [4]
	typeRef(scopeGoclrRuntime, sBuiltins, sRuntimeNS)   // [5]
	typeRef(scopeGoclrRuntime, sGoString, sRuntimeNS)   // [6]
	typeRef(scopeGoclrRuntime, sGoStrings, sRuntimeNS)  // [7]
	typeRef(scopeSysRuntime, sValueType, sSystem)       // [8] System.ValueType
	typeRef(scopeGoclrRuntime, sGoSlice, sRuntimeNS)    // [9] GoSlice
	typeRef(scopeGoclrRuntime, sGoSlices, sRuntimeNS)   // [10] GoSlices
	typeRef(scopeGoclrRuntime, sGoMap, sRuntimeNS)      // [11] GoMap
	typeRef(scopeGoclrRuntime, sGoMaps, sRuntimeNS)     // [12] GoMaps
	typeRef(scopeGoclrRuntime, sGoPtr, sRuntimeNS)      // [13] GoPtr
	typeRef(scopeGoclrRuntime, sGoPtrs, sRuntimeNS)     // [14] GoPtrs
	typeRef(scopeGoclrRuntime, sGoPanic, sRuntimeNS)    // [15] GoPanicException
	typeRef(scopeGoclrRuntime, sGoClosure, sRuntimeNS)  // [16] GoClosure
	typeRef(scopeGoclrRuntime, sGoClosures, sRuntimeNS) // [17] GoClosures
	typeRef(scopeSysRuntime, sDouble, sSystem)          // [18] System.Double
	typeRef(scopeSysRuntime, sSingle, sSystem)          // [19] System.Single
	typeRef(scopeSysRuntime, sUInt64, sSystem)          // [20] System.UInt64
	typeRef(scopeSysRuntime, sUInt32, sSystem)          // [21] System.UInt32
	typeRef(scopeGoclrRuntime, sGoChan, sRuntimeNS)     // [22] GoChan
	typeRef(scopeGoclrRuntime, sGoChans, sRuntimeNS)    // [23] GoChans
	typeRef(scopeGoclrRuntime, sGoRuntime, sRuntimeNS)  // [24] GoRuntime
	typeRef(scopeGoclrRuntime, sGoInvoker, sRuntimeNS)  // [25] GoInvoker
	typeRef(scopeGoclrRuntime, sGoSelect, sRuntimeNS)   // [26] GoSelect
	typeRef(scopeGoclrRuntime, sGoComplex, sRuntimeNS)  // [27] GoComplex
	typeRef(scopeGoclrRuntime, sGoComplexs, sRuntimeNS) // [28] GoComplexs
	typeRef(scopeGoclrRuntime, sGoDefers, sRuntimeNS)   // [29] GoDefers
	typeRef(scopeGoclrRuntime, sGoError, sRuntimeNS)    // [30] GoError
	typeRef(scopeGoclrRuntime, sGoErrors, sRuntimeNS)   // [31] GoErrors
	for _, et := range extTypes {                       // [32+] shim types
		typeRef(et.scope, et.name, et.ns)
	}

	// --- TypeDef (0x02) ---
	// [1] <Module>.
	w.u32(0)
	w.heap(sModuleType)
	w.heap(sEmpty)
	w.idx(0, wTypeDefOrRef) // extends null
	w.idx(1, wFieldIdx)     // FieldList
	w.idx(1, wMethodIdx)    // MethodList (no methods)
	// [2] Program : System.Object — owns all methods.
	w.u32(0x00100001) // Public | BeforeFieldInit
	w.heap(sProgram)
	w.heap(sEmpty)
	w.idx(extendsObject, wTypeDefOrRef)
	w.idx(1, wFieldIdx)  // FieldList (owns none; structs' fields start at 1)
	w.idx(1, wMethodIdx) // MethodList -> MethodDef[1] (owns all methods)
	// [3..] struct value types, extending System.ValueType.
	extendsValueType := uint32(8<<2 | 1)             // TypeDefOrRef -> TypeRef[8]
	structMethodList := uint32(len(prog.Methods) + 1) // structs own no methods
	for i := range prog.Structs {
		w.u32(0x00100109) // Public | SequentialLayout | Sealed | BeforeFieldInit
		w.heap(structNameIdx[i])
		w.heap(sEmpty)
		w.idx(extendsValueType, wTypeDefOrRef)
		w.idx(uint32(structFieldStart[i]), wFieldIdx) // FieldList
		w.idx(structMethodList, wMethodIdx)           // MethodList
	}

	// --- Field (0x04) ---
	for _, fr := range fieldRows {
		flags := uint16(0x0006) // Public
		if fr.static {
			flags = 0x0016 // Public | Static
		}
		w.u16(flags)
		w.heap(fr.name)
		w.heap(fr.sig)
	}

	// --- MethodDef (0x06) ---
	for i := range prog.Methods {
		w.u32(methodRVAs[i])     // RVA
		w.u16(0)                 // ImplFlags
		w.u16(0x0016)            // Public | Static
		w.heap(methodNameIdx[i]) // Name
		w.heap(methodSigIdx[i])  // Signature
		w.idx(1, wParamIdx)      // ParamList (Param table empty)
	}

	// --- MemberRef (0x0A) ---
	sPrintlnName := h.addString("Println")
	sPrintName := h.addString("Print")
	w.idx(parentBuiltins, wMemberRefParent) // [1] Println
	w.heap(sPrintlnName)
	w.heap(bPrintln)
	w.idx(parentBuiltins, wMemberRefParent) // [2] Print
	w.heap(sPrintName)
	w.heap(bPrintln)
	for i := range strMembers { // GoStrings.* (incl. To{Byte,Rune}Slice)
		w.idx(parentGoStrings, wMemberRefParent)
		w.heap(strMemberName[i])
		w.heap(strMemberSig[i])
	}
	for i := range sliceMembers { // GoSlices.*
		w.idx(parentGoSlices, wMemberRefParent)
		w.heap(sliceMemberName[i])
		w.heap(sliceMemberSig[i])
	}
	for i := range mapMembers { // GoMaps.*
		w.idx(parentGoMaps, wMemberRefParent)
		w.heap(mapMemberName[i])
		w.heap(mapMemberSig[i])
	}
	for i := range ptrMembers { // GoPtrs.*
		w.idx(parentGoPtrs, wMemberRefParent)
		w.heap(ptrMemberName[i])
		w.heap(ptrMemberSig[i])
	}
	// [30..33] Builtins.Panic / Recover / SetPanic / PanicHandled.
	w.idx(parentBuiltins, wMemberRefParent)
	w.heap(h.addString("Panic"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x01, 0x1C})) // void(object)
	w.idx(parentBuiltins, wMemberRefParent)
	w.heap(h.addString("Recover"))
	w.heap(h.addBlob([]byte{0x00, 0x00, 0x1C})) // object()
	w.idx(parentBuiltins, wMemberRefParent)
	w.heap(h.addString("SetPanic"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, 0x3D})) // void(class GoPanicException)
	w.idx(parentBuiltins, wMemberRefParent)
	w.heap(h.addString("PanicHandled"))
	w.heap(h.addBlob([]byte{0x00, 0x00, 0x02})) // bool()
	// [34..36] GoClosures.New / Id / Env.
	parentGoClosures := uint32(17<<3 | 1)
	w.idx(parentGoClosures, wMemberRefParent)
	w.heap(h.addString("New"))
	w.heap(h.addBlob([]byte{0x00, 0x02, 0x12, goClosureCoded, 0x0A, 0x1D, 0x1C})) // GoClosure(i8, object[])
	w.idx(parentGoClosures, wMemberRefParent)
	w.heap(h.addString("Id"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x0A, 0x12, goClosureCoded})) // i8(GoClosure)
	w.idx(parentGoClosures, wMemberRefParent)
	w.heap(h.addString("Env"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x1D, 0x1C, 0x12, goClosureCoded})) // object[](GoClosure)
	// [37..43] GoChans.* (Make/Send/Recv/Recv2/Close/Len/Cap).
	parentGoChans := uint32(23<<3 | 1)
	for i := range chanMembers {
		w.idx(parentGoChans, wMemberRefParent)
		w.heap(chanMemberName[i])
		w.heap(chanMemberSig[i])
	}
	// [44] GoRuntime.Go(GoClosure).
	parentGoRuntime := uint32(24<<3 | 1)
	w.idx(parentGoRuntime, wMemberRefParent)
	w.heap(h.addString("Go"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, goClosureCoded})) // void(GoClosure)
	// [45] GoRuntime.SetInvoker(GoInvoker).
	w.idx(parentGoRuntime, wMemberRefParent)
	w.heap(h.addString("SetInvoker"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, goInvokerCoded})) // void(GoInvoker)
	// [46] GoInvoker::.ctor(object, native int) — delegate constructor.
	parentGoInvoker := uint32(25<<3 | 1)
	w.idx(parentGoInvoker, wMemberRefParent)
	w.heap(h.addString(".ctor"))
	w.heap(h.addBlob([]byte{0x20, 0x02, 0x01, 0x1C, 0x18})) // HASTHIS void(object, native int)
	// [47] GoSelect.Select(object[], object[], object[], bool) -> object[].
	parentGoSelect := uint32(26<<3 | 1)
	w.idx(parentGoSelect, wMemberRefParent)
	w.heap(h.addString("Select"))
	w.heap(h.addBlob([]byte{0x00, 0x04, 0x1D, 0x1C, 0x1D, 0x1C, 0x1D, 0x1C, 0x1D, 0x1C, 0x02}))
	// [48] GoPtrs.TypeIdOf(GoPtr) -> i8 (appended last to keep earlier tokens stable).
	w.idx(parentGoPtrs, wMemberRefParent)
	w.heap(h.addString("TypeIdOf"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x0A, 0x12, goPtrCoded})) // i8(pt)
	// [49..57] GoComplexs.* (Make/Add/Sub/Mul/Div/Neg/Eq/Real/Imag).
	parentGoComplexs := uint32(28<<3 | 1)
	cplx := func(name string, blob []byte) {
		w.idx(parentGoComplexs, wMemberRefParent)
		w.heap(h.addString(name))
		w.heap(h.addBlob(blob))
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
	parentGoDefers := uint32(29<<3 | 1)
	w.idx(parentGoDefers, wMemberRefParent)
	w.heap(h.addString("Mark"))
	w.heap(h.addBlob([]byte{0x00, 0x00, 0x0A})) // i8()
	w.idx(parentGoDefers, wMemberRefParent)
	w.heap(h.addString("Push"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x01, 0x12, goClosureCoded})) // void(GoClosure)
	w.idx(parentGoDefers, wMemberRefParent)
	w.heap(h.addString("Run"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x01, 0x0A})) // void(i8)
	// [61] GoErrors.Error(GoError) -> GoString.
	w.idx(uint32(31<<3 | 1), wMemberRefParent) // MemberRefParent -> TypeRef[31] GoErrors
	w.heap(h.addString("Error"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x11, goStringCoded, 0x12, goErrorCoded})) // GoString(GoError)
	// [62..64] GoStrings.FromRune/FromBytes/FromRunes.
	w.idx(parentGoStrings, wMemberRefParent)
	w.heap(h.addString("FromRune"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x11, goStringCoded, 0x0A})) // GoString(i8)
	w.idx(parentGoStrings, wMemberRefParent)
	w.heap(h.addString("FromBytes"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x11, goStringCoded, 0x11, goSliceCoded})) // GoString(sl)
	w.idx(parentGoStrings, wMemberRefParent)
	w.heap(h.addString("FromRunes"))
	w.heap(h.addBlob([]byte{0x00, 0x01, 0x11, goStringCoded, 0x11, goSliceCoded})) // GoString(sl)
	for _, em := range extMembers {                                                // [65+] shim methods
		w.idx(em.parent, wMemberRefParent)
		w.heap(em.name)
		w.heap(em.sig)
	}

	// --- StandAloneSig (0x11) ---
	if hasLocals {
		for _, off := range sigBlobOffsets {
			w.heap(off)
		}
	}

	// --- Assembly (0x20) ---
	w.u32(0x8004) // SHA1
	w.u16(1)      // Major
	w.u16(0)
	w.u16(0)
	w.u16(0)
	w.u32(0)     // Flags
	w.heap(0)    // PublicKey (#Blob)
	w.heap(sAsm) // Name (#Strings)
	w.heap(0)    // Culture (#Strings)

	// --- AssemblyRef (0x23) ---
	// [1] System.Runtime 8.0.0.0.
	w.u16(8)
	w.u16(0)
	w.u16(0)
	w.u16(0)
	w.u32(0)
	w.heap(bEcmaToken)  // PublicKeyOrToken (#Blob)
	w.heap(sSysRuntime) // Name (#Strings)
	w.heap(0)           // Culture (#Strings)
	w.heap(0)           // HashValue (#Blob)
	// [2] GoCLR.Runtime 1.0.0.0.
	w.u16(1)
	w.u16(0)
	w.u16(0)
	w.u16(0)
	w.u32(0)
	w.heap(0)             // PublicKeyOrToken (#Blob)
	w.heap(sGoclrRuntime) // Name (#Strings)
	w.heap(0)             // Culture (#Strings)
	w.heap(0)             // HashValue (#Blob)
	// [3+] shim assemblies (GoCLR.Stdlib, ...) 1.0.0.0.
	for _, name := range extAsmName {
		w.u16(1)
		w.u16(0)
		w.u16(0)
		w.u16(0)
		w.u32(0)
		w.heap(0)    // PublicKeyOrToken (#Blob)
		w.heap(name) // Name (#Strings)
		w.heap(0)    // Culture (#Strings)
		w.heap(0)    // HashValue (#Blob)
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
