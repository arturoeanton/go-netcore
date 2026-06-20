// Package goir is the GoCLR intermediate representation: a backend-friendly,
// linear, typed view of a Go program consumed by the CIL emitter.
//
// M1 models multiple static methods (one per Go function) with typed parameters,
// locals, and a flat instruction stream that uses symbolic labels for control
// flow, plus user-defined struct value types. Lowering (internal/lower) produces
// it from go/ast + go/types; the emitter (internal/emit) turns it into CIL.
package goir

// Kind enumerates the CLR type kinds the M1 backend handles.
type Kind int

const (
	KVoid Kind = iota
	KInt64
	KInt32
	KUint64
	KUint32
	KFloat64
	KFloat32
	KBool
	KString // Go string -> GoString value type
	KObject
	KObjectArray // object[] — a boxed multi-return tuple
	KStruct      // a user-defined struct value type
	KSlice       // a Go slice -> GoSlice (object[]-backed)
	KMap         // a Go map -> GoMap (Dictionary-backed reference type)
	KPtr         // a Go pointer -> GoPtr cell (reference type)
	KFunc        // a Go function value -> GoClosure (reference type)
	KChan        // a Go channel -> GoChan (reference type)
	KComplex     // a Go complex128/complex64 -> GoComplex {Re, Im} (reference type)
)

// Type is a type in the IR. Struct is set iff Kind == KStruct; Elem iff KSlice;
// Key/Val iff KMap.
type Type struct {
	Kind   Kind
	Struct *Struct
	Elem   *Type
	Key    *Type
	Val    *Type
	// Shim names an opaque stdlib value type backed by a runtime reference object
	// (e.g. "sync.WaitGroup"); Kind is KObject. Its zero value is a fresh object
	// built by a registered constructor rather than null.
	Shim string
	// Array marks a fixed-size [N]T array (Kind is KSlice, since arrays are
	// slice-backed). Unlike a slice, an array has value semantics: copying it (on
	// assignment, argument passing, return) duplicates its backing storage.
	Array bool
}

// Predeclared primitive types. They are package-level values (not consts) so
// they compare equal field-wise: e.g. `t == TInt64` holds for any int64 Type.
var (
	TVoid        = Type{Kind: KVoid}
	TInt64       = Type{Kind: KInt64}
	TInt32       = Type{Kind: KInt32}
	TUint64      = Type{Kind: KUint64}
	TUint32      = Type{Kind: KUint32}
	TFloat64     = Type{Kind: KFloat64}
	TFloat32     = Type{Kind: KFloat32}
	TBool        = Type{Kind: KBool}
	TString      = Type{Kind: KString}
	TObject      = Type{Kind: KObject}
	TObjectArray = Type{Kind: KObjectArray}
	TComplex     = Type{Kind: KComplex}
)

// StructType wraps a struct descriptor as a Type.
func StructType(s *Struct) Type { return Type{Kind: KStruct, Struct: s} }

// SliceType builds a slice type with the given element type.
func SliceType(elem Type) Type { return Type{Kind: KSlice, Elem: &elem} }

// MapType builds a map type with the given key and value types.
func MapType(key, val Type) Type { return Type{Kind: KMap, Key: &key, Val: &val} }

// PtrType builds a pointer type with the given pointee type (stored in Elem).
func PtrType(elem Type) Type { return Type{Kind: KPtr, Elem: &elem} }

// TFunc is the type of any Go function value (a GoClosure reference).
var TFunc = Type{Kind: KFunc}

// ChanType builds a channel type with the given element type (stored in Elem).
func ChanType(elem Type) Type { return Type{Kind: KChan, Elem: &elem} }

// Struct describes a user-defined struct value type.
type Struct struct {
	Name   string // emitted CLR type name
	GoName string // original Go type name
	Fields []Field
	// TypeDefRow is the struct's TypeDef row, assigned by the emitter before
	// signatures are built so type references can be encoded.
	TypeDefRow int
	// Id is a stable 1-based identifier assigned at registration. It tags GoPtr
	// cells so pointer-receiver implementers can be matched at interface dispatch.
	Id int
}

// Field is a struct field.
type Field struct {
	Name string
	Type Type
	Tag  string // the raw Go struct tag (for reflect/json), without backticks
}

// FieldIndex returns the index of a field by name, or -1.
func (s *Struct) FieldIndex(name string) int {
	for i, f := range s.Fields {
		if f.Name == name {
			return i
		}
	}
	return -1
}

// Program is a whole compiled Go program targeting one .NET assembly.
type Program struct {
	AssemblyName string
	Structs      []*Struct // user-defined struct types (TypeDef order)
	Methods      []*Method // static methods of the Program type
	Globals      []*Global // package-level variables -> static fields on Program
	Entry        *Method   // the lowered main.main
	Invoke       *Method   // the closure dispatcher (__invoke), if any closures/goroutines exist
}

// Global is a package-level variable, emitted as a static field on the Program
// type. Its 0-based index is the order in Program.Globals.
type Global struct {
	Name string
	Type Type
}

// Method is a lowered Go function emitted as a static CLR method.
type Method struct {
	Name    string
	GoName  string
	Params  []Type
	Ret     Type
	Results []Type // expanded Go result types (len>1 => Ret is object[])
	Locals  []Type
	Code    []Op
	EH      []EHClause // exception-handling regions (try/catch on GoPanicException)
}

// EHClause is a try/catch region. The fields are label ids marking the region
// boundaries in Code; the emitter resolves them to IL offsets.
type EHClause struct {
	TryStart, TryEnd         int
	HandlerStart, HandlerEnd int
}

// Opcode enumerates the linear IR operations.
type Opcode int

const (
	OpNop Opcode = iota

	OpLdcI8
	OpLdcI4
	OpLdcR8 // push Float as float64
	OpLdcR4 // push Float as float32
	OpLdStr

	OpLdLoc
	OpStLoc
	OpLdArg
	OpLdLocA // ldloca: push the address of a local (for stfld / initobj)

	OpAdd
	OpSub
	OpMul
	OpDiv
	OpRem
	OpNeg
	OpAnd
	OpOr
	OpXor
	OpShl
	OpShr

	OpCeq
	OpClt
	OpCgt
	OpCltUn // unsigned/unordered less-than
	OpCgtUn // unsigned/unordered greater-than
	OpNot

	OpDivUn // unsigned division
	OpRemUn // unsigned remainder
	OpShrUn // unsigned (logical) shift right

	OpConvI8
	OpConvI4
	OpConvR8 // conv.r8 (to float64)
	OpConvR4 // conv.r4 (to float32)
	OpConvU8 // conv.u8 (to uint64)
	OpConvU4 // conv.u4 (to uint32)

	OpNewObjArray
	OpDup
	OpStelemRef
	OpLdElemRef
	OpBox

	OpCallPrintln
	OpCallPrint
	OpCallPanic        // Builtins.Panic(object)
	OpCallRecover      // Builtins.Recover() -> object
	OpCallSetPanic     // Builtins.SetPanic(GoPanicException)
	OpCallPanicHandled // Builtins.PanicHandled() -> bool
	OpLeave            // leave Label (exit a try/catch region)
	OpRethrow          // rethrow the current exception
	OpClosNew          // id(i64), env(object[]) -> GoClosure
	OpClosId           // GoClosure -> i64
	OpClosEnv          // GoClosure -> object[]
	OpCallMethod

	OpStrConst
	OpStrLen
	OpStrIndex
	OpStrConcat
	OpStrEqual
	OpStrCompare
	OpStrRuneAt
	OpStrRuneSize

	// Struct operations.
	OpLdFld   // pop struct (value or addr), push field Field of Struct
	OpLdFldA  // pop addr, push address of field Field of Struct
	OpStFld   // pop addr, value; store into field Field of Struct
	OpInitObj // pop addr; zero-initialize the Struct in place

	// Slice operations (GoSlice via the GoSlices runtime helper). Elements are
	// boxed in the backing array; OpUnbox/OpBox bridge to the typed value.
	OpSliceMake   // len, cap, boxed-zero -> GoSlice
	OpSliceGet    // GoSlice, i64 -> object (boxed element)
	OpSliceSet    // GoSlice, i64, object ->
	OpSliceLen    // GoSlice -> i64
	OpSliceCap    // GoSlice -> i64
	OpSliceAppend // GoSlice, object -> GoSlice
	OpSliceSlice  // GoSlice, lo, hi -> GoSlice
	OpUnbox       // object -> value of BoxTy (unbox.any)
	OpStrToBytes  // GoString -> GoSlice ([]byte)
	OpStrToRunes  // GoString -> GoSlice ([]rune)

	OpIsInst // object -> object typed as BoxTy, or null (isinst)

	// Map operations (GoMap via the GoMaps runtime helper).
	OpMapMake     // -> GoMap
	OpMapGet      // GoMap, key, boxed-zero -> object (boxed value or zero)
	OpMapContains // GoMap, key -> bool
	OpMapSet      // GoMap, key, value ->
	OpMapDelete   // GoMap, key ->
	OpMapLen      // GoMap -> i64
	OpMapKeys     // GoMap -> GoSlice (boxed keys)
	OpLdNull      // push null (nil reference)

	// Pointer operations (GoPtr cell via the GoPtrs runtime helper).
	OpPtrNew    // object -> GoPtr (allocate a cell; Op.Int = pointee struct type id)
	OpPtrGet    // GoPtr -> object (boxed pointee)
	OpPtrSet    // GoPtr, object ->
	OpPtrTypeId // GoPtr -> i8 (the cell's pointee type id)

	// Channel operations (GoChan via the GoChans runtime helper).
	OpChanMake  // i64 cap -> GoChan
	OpChanSend  // GoChan, object ->
	OpChanRecv  // GoChan -> object (boxed element)
	OpChanRecv2 // GoChan -> object[] {boxed value, boxed ok}
	OpChanClose // GoChan ->
	OpChanLen   // GoChan -> i64
	OpChanCap   // GoChan -> i64

	// Goroutine operations (GoRuntime via the runtime helper).
	OpGoStart         // GoClosure -> (spawns the closure on a background task)
	OpRegisterInvoker // -> (registers the closure dispatcher with GoRuntime at startup)

	// Defer operations (GoDefers runtime stack).
	OpDeferMark // -> i8 (current defer-stack depth, the function's entry mark)
	OpDeferPush // GoClosure -> (push a deferred thunk)
	OpDeferRun  // i8 -> (run defers LIFO down to the mark)

	OpSelect // chans, ops, sendVals, hasDefault -> object[]{index, value, ok}

	// Complex operations (GoComplex via the GoComplexs runtime helper).
	OpComplexMake // r8 re, r8 im -> GoComplex
	OpComplexAdd  // GoComplex, GoComplex -> GoComplex
	OpComplexSub  // GoComplex, GoComplex -> GoComplex
	OpComplexMul  // GoComplex, GoComplex -> GoComplex
	OpComplexDiv  // GoComplex, GoComplex -> GoComplex
	OpComplexNeg  // GoComplex -> GoComplex
	OpComplexEq   // GoComplex, GoComplex -> bool
	OpComplexReal // GoComplex -> r8
	OpComplexImag // GoComplex -> r8

	OpCallExtern // call an external (shim) static method described by Op.Extern

	OpIsInstGoError // object -> object typed as GoError, or null (isinst GoError)
	OpErrorError    // GoError -> GoString (GoErrors.Error: the error interface's Error())

	OpStrFromRune  // i8 -> GoString (string(rune))
	OpStrFromBytes // GoSlice -> GoString (string([]byte))
	OpStrFromRunes // GoSlice -> GoString (string([]rune))

	OpLdGlobal // -> value of global Op.Int (ldsfld)
	OpStGlobal // value -> ; store into global Op.Int (stsfld)

	OpLabel
	OpBr
	OpBrTrue
	OpBrFalse

	OpRet
	OpPop
)

// Op is a single IR instruction. Only the fields relevant to Code are set.
type Op struct {
	Code   Opcode
	Int    int64
	Float  float64
	Str    string
	Local  int
	Arg    int
	Label  int
	Callee *Method
	BoxTy  Type

	// Struct field access (OpLdFld/OpStFld/OpInitObj).
	Struct *Struct
	Field  int

	// External shim call (OpCallExtern).
	Extern *Extern
}

// Extern describes a static method in a shim assembly (e.g. GoCLR.Stdlib) that a
// Go stdlib call lowers to. Params/Ret are the IR types used to build its
// signature and stack effect.
type Extern struct {
	Assembly  string // e.g. "GoCLR.Stdlib"
	Namespace string // e.g. "GoCLR.Stdlib"
	Type      string // e.g. "Math"
	Method    string // e.g. "Sqrt"
	Params    []Type
	Ret       Type
}

// Key uniquely identifies an extern for dedup in the emitter.
func (e *Extern) Key() string {
	return e.Assembly + " " + e.Namespace + "." + e.Type + "." + e.Method
}

// TypeKey identifies the extern's declaring type (for TypeRef dedup).
func (e *Extern) TypeKey() string {
	return e.Assembly + " " + e.Namespace + "." + e.Type
}
