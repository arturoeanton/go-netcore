package lower

import (
	"go/ast"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// shimAssembly is the managed assembly holding the stdlib shims.
const shimAssembly = "GoCLR.Stdlib"

// shimFunc names the C# type and method a Go stdlib function lowers to.
type shimFunc struct {
	csType   string
	csMethod string
}

// shimRegistry maps a stdlib import path to the functions implemented as C#
// shims (Go func name -> C# type/method in GoCLR.Stdlib). Argument and result
// types are derived from the Go signature, so only the binding is listed here.
var shimRegistry = map[string]map[string]shimFunc{
	"math": {
		"Abs": {"Math", "Abs"}, "Acos": {"Math", "Acos"}, "Asin": {"Math", "Asin"},
		"Atan": {"Math", "Atan"}, "Atan2": {"Math", "Atan2"}, "Cbrt": {"Math", "Cbrt"},
		"Ceil": {"Math", "Ceil"}, "Copysign": {"Math", "Copysign"}, "Cos": {"Math", "Cos"},
		"Cosh": {"Math", "Cosh"}, "Exp": {"Math", "Exp"}, "Exp2": {"Math", "Exp2"},
		"Floor": {"Math", "Floor"}, "Hypot": {"Math", "Hypot"}, "Log": {"Math", "Log"},
		"Log10": {"Math", "Log10"}, "Log2": {"Math", "Log2"}, "Max": {"Math", "Max"},
		"Min": {"Math", "Min"}, "Mod": {"Math", "Mod"}, "Pow": {"Math", "Pow"},
		"Remainder": {"Math", "Remainder"}, "Round": {"Math", "Round"}, "Signbit": {"Math", "Signbit"},
		"Sin": {"Math", "Sin"}, "Sinh": {"Math", "Sinh"}, "Sqrt": {"Math", "Sqrt"},
		"Tan": {"Math", "Tan"}, "Tanh": {"Math", "Tanh"}, "Trunc": {"Math", "Trunc"},
		"IsNaN": {"Math", "IsNaN"}, "IsInf": {"Math", "IsInf"}, "NaN": {"Math", "NaN"},
		"Inf": {"Math", "Inf"},
	},
	"errors": {
		"New": {"Errors", "New"}, "Unwrap": {"Errors", "Unwrap"}, "Is": {"Errors", "Is"},
	},
	"unicode": {
		"IsDigit": {"Unicode", "IsDigit"}, "IsNumber": {"Unicode", "IsNumber"}, "IsLetter": {"Unicode", "IsLetter"},
		"IsSpace": {"Unicode", "IsSpace"}, "IsUpper": {"Unicode", "IsUpper"}, "IsLower": {"Unicode", "IsLower"},
		"IsPunct": {"Unicode", "IsPunct"}, "IsControl": {"Unicode", "IsControl"}, "IsPrint": {"Unicode", "IsPrint"},
		"IsGraphic": {"Unicode", "IsGraphic"}, "ToUpper": {"Unicode", "ToUpper"}, "ToLower": {"Unicode", "ToLower"},
		"ToTitle": {"Unicode", "ToTitle"},
	},
	"reflect": {
		"TypeOf": {"Reflect", "TypeOf"}, "ValueOf": {"Reflect", "ValueOf"}, "DeepEqual": {"Reflect", "DeepEqual"},
	},
	"encoding/json": {
		"Marshal": {"Json", "Marshal"},
	},
	"fmt": {
		"Sprint": {"Fmt", "Sprint"}, "Sprintln": {"Fmt", "Sprintln"}, "Sprintf": {"Fmt", "Sprintf"},
		"Print": {"Fmt", "Print"}, "Println": {"Fmt", "Println"}, "Printf": {"Fmt", "Printf"},
		"Errorf": {"Fmt", "Errorf"},
		"Fprint": {"Fmt", "Fprint"}, "Fprintln": {"Fmt", "Fprintln"}, "Fprintf": {"Fmt", "Fprintf"},
	},
	"io": {
		"WriteString": {"Io", "WriteString"},
	},
	"sort": {
		"Ints": {"Sort", "Ints"}, "Float64s": {"Sort", "Float64s"}, "Strings": {"Sort", "Strings"},
		"IntsAreSorted": {"Sort", "IntsAreSorted"}, "SearchInts": {"Sort", "SearchInts"},
	},
	"time": {
		"Sleep": {"Time", "Sleep"},
		"Now": {"Time", "Now"}, "Unix": {"Time", "Unix"}, "Date": {"Time", "Date"}, "Since": {"Time", "Since"},
	},
	"math/bits": {
		"OnesCount": {"MathBits", "OnesCount"}, "OnesCount64": {"MathBits", "OnesCount64"}, "OnesCount32": {"MathBits", "OnesCount32"},
		"LeadingZeros": {"MathBits", "LeadingZeros"}, "LeadingZeros64": {"MathBits", "LeadingZeros64"},
		"TrailingZeros": {"MathBits", "TrailingZeros"}, "TrailingZeros64": {"MathBits", "TrailingZeros64"},
		"Len": {"MathBits", "Len"}, "Len64": {"MathBits", "Len64"}, "RotateLeft64": {"MathBits", "RotateLeft64"},
		"Reverse64": {"MathBits", "Reverse64"}, "ReverseBytes64": {"MathBits", "ReverseBytes64"},
	},
	"os": {
		"Getenv": {"Os", "Getenv"}, "LookupEnv": {"Os", "LookupEnv"}, "Setenv": {"Os", "Setenv"},
		"Unsetenv": {"Os", "Unsetenv"}, "Exit": {"Os", "Exit"}, "Getpid": {"Os", "Getpid"},
	},
	"bytes": {
		"Equal": {"Bytes", "Equal"}, "Compare": {"Bytes", "Compare"}, "Contains": {"Bytes", "Contains"},
		"HasPrefix": {"Bytes", "HasPrefix"}, "HasSuffix": {"Bytes", "HasSuffix"}, "Index": {"Bytes", "Index"},
		"IndexByte": {"Bytes", "IndexByte"}, "Count": {"Bytes", "Count"}, "ToUpper": {"Bytes", "ToUpper"},
		"ToLower": {"Bytes", "ToLower"}, "TrimSpace": {"Bytes", "TrimSpace"}, "Repeat": {"Bytes", "Repeat"},
		"Split": {"Bytes", "Split"}, "Join": {"Bytes", "Join"},
	},
	"strconv": {
		"Itoa": {"Strconv", "Itoa"}, "Atoi": {"Strconv", "Atoi"},
		"FormatInt": {"Strconv", "FormatInt"}, "FormatUint": {"Strconv", "FormatUint"},
		"FormatBool": {"Strconv", "FormatBool"}, "FormatFloat": {"Strconv", "FormatFloat"},
		"ParseInt": {"Strconv", "ParseInt"}, "ParseUint": {"Strconv", "ParseUint"},
		"ParseFloat": {"Strconv", "ParseFloat"}, "ParseBool": {"Strconv", "ParseBool"},
		"Quote": {"Strconv", "Quote"},
	},
	"unicode/utf8": {
		"RuneCountInString": {"Utf8", "RuneCountInString"}, "RuneCount": {"Utf8", "RuneCount"},
		"ValidString": {"Utf8", "ValidString"}, "ValidRune": {"Utf8", "ValidRune"}, "RuneLen": {"Utf8", "RuneLen"},
	},
	"strings": {
		"ToUpper": {"Strings", "ToUpper"}, "ToLower": {"Strings", "ToLower"}, "Title": {"Strings", "Title"},
		"Contains": {"Strings", "Contains"}, "HasPrefix": {"Strings", "HasPrefix"}, "HasSuffix": {"Strings", "HasSuffix"},
		"EqualFold": {"Strings", "EqualFold"}, "Index": {"Strings", "Index"}, "LastIndex": {"Strings", "LastIndex"},
		"IndexByte": {"Strings", "IndexByte"}, "Count": {"Strings", "Count"}, "Repeat": {"Strings", "Repeat"},
		"Replace": {"Strings", "Replace"}, "ReplaceAll": {"Strings", "ReplaceAll"}, "TrimSpace": {"Strings", "TrimSpace"},
		"Trim": {"Strings", "Trim"}, "TrimLeft": {"Strings", "TrimLeft"}, "TrimRight": {"Strings", "TrimRight"},
		"TrimPrefix": {"Strings", "TrimPrefix"}, "TrimSuffix": {"Strings", "TrimSuffix"},
		"Split": {"Strings", "Split"}, "SplitN": {"Strings", "SplitN"}, "Fields": {"Strings", "Fields"},
		"Join": {"Strings", "Join"},
	},
}

// opaqueShimTypes are stdlib types represented at runtime as opaque object
// handles (not lowered structures); method calls on them dispatch to shims.
var opaqueShimTypes = map[string]bool{
	"reflect.Type":      true,
	"reflect.Value":     true,
	"sync.Mutex":        true,
	"sync.RWMutex":      true,
	"sync.WaitGroup":    true,
	"sync.Once":         true,
	"sync.Map":          true,
	"strings.Builder":   true,
	"bytes.Buffer":      true,
	"os.File":           true,
	"time.Time":         true,
	"time.Location":     true,
}

// shimVarRegistry maps "importpath.VarName" stdlib package variables to a
// no-argument accessor returning the runtime object.
var shimVarRegistry = map[string]shimFunc{
	"os.Stdout":  {"Os", "Stdout"},
	"os.Stderr":  {"Os", "Stderr"},
	"time.UTC":   {"Time", "UTC"},
	"time.Local": {"Time", "Local"},
}

// shimVarExtern returns the accessor extern for a shimmed stdlib package variable
// reference (e.g. os.Stdout), if e is one.
func (l *funcLowerer) shimVarExtern(e ast.Expr) (*goir.Extern, bool) {
	sel, ok := e.(*ast.SelectorExpr)
	if !ok {
		return nil, false
	}
	v, ok := l.pkg.TypesInfo.Uses[sel.Sel].(*types.Var)
	if !ok || v.Pkg() == nil {
		return nil, false
	}
	sf, ok := shimVarRegistry[v.Pkg().Path()+"."+v.Name()]
	if !ok {
		return nil, false
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Ret: goir.TObject}, true
}

// opaqueZeroCtor maps an opaque value-type shim to the constructor producing its
// (non-null) zero value; types absent here zero to null (e.g. reflect handles).
var opaqueZeroCtor = map[string]shimFunc{
	"sync.Mutex":      {"Sync", "NewMutex"},
	"sync.RWMutex":    {"Sync", "NewRWMutex"},
	"sync.WaitGroup":  {"Sync", "NewWaitGroup"},
	"sync.Once":       {"Sync", "NewOnce"},
	"sync.Map":        {"Sync", "NewMap"},
	"strings.Builder": {"StringsBuilder", "New"},
	"bytes.Buffer":    {"BytesBuffer", "New"},
	"time.Time":       {"Time", "TimeZero"},
}

// shimZeroExtern returns the zero-value constructor extern for an opaque value
// type shim, if it has one.
func shimZeroExtern(shim string) (*goir.Extern, bool) {
	sf, ok := opaqueZeroCtor[shim]
	if !ok {
		return nil, false
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Ret: goir.TObject}, true
}

// isOpaqueShimType reports whether a named type is an opaque shim handle.
func isOpaqueShimType(named *types.Named) bool {
	obj := named.Obj()
	if obj == nil || obj.Pkg() == nil {
		return false
	}
	return opaqueShimTypes[obj.Pkg().Path()+"."+obj.Name()]
}

// shimMethodRegistry maps "importpath.TypeName" to its shimmed methods
// (Go method name -> C# type/static-method). The C# method takes the receiver as
// its first argument.
var shimMethodRegistry = map[string]map[string]shimFunc{
	"reflect.Type": {
		"Kind": {"Reflect", "Type_Kind"}, "Name": {"Reflect", "Type_Name"},
		"String": {"Reflect", "Type_String"}, "NumField": {"Reflect", "Type_NumField"},
		"Elem": {"Reflect", "Type_Elem"},
	},
	"strings.Builder": {
		"WriteString": {"StringsBuilder", "WriteString"}, "WriteByte": {"StringsBuilder", "WriteByte"},
		"WriteRune": {"StringsBuilder", "WriteRune"}, "Write": {"StringsBuilder", "Write"},
		"String": {"StringsBuilder", "String"}, "Len": {"StringsBuilder", "Len"},
		"Cap": {"StringsBuilder", "Cap"}, "Reset": {"StringsBuilder", "Reset"}, "Grow": {"StringsBuilder", "Grow"},
	},
	"bytes.Buffer": {
		"WriteString": {"BytesBuffer", "WriteString"}, "WriteByte": {"BytesBuffer", "WriteByte"},
		"Write": {"BytesBuffer", "Write"}, "String": {"BytesBuffer", "String"},
		"Bytes": {"BytesBuffer", "Bytes"}, "Len": {"BytesBuffer", "Len"}, "Reset": {"BytesBuffer", "Reset"},
	},
	"sync.Mutex": {
		"Lock": {"Sync", "Mutex_Lock"}, "Unlock": {"Sync", "Mutex_Unlock"}, "TryLock": {"Sync", "Mutex_TryLock"},
	},
	"sync.RWMutex": {
		"Lock": {"Sync", "RWMutex_Lock"}, "Unlock": {"Sync", "RWMutex_Unlock"},
		"RLock": {"Sync", "RWMutex_RLock"}, "RUnlock": {"Sync", "RWMutex_RUnlock"},
	},
	"sync.WaitGroup": {
		"Add": {"Sync", "WaitGroup_Add"}, "Done": {"Sync", "WaitGroup_Done"}, "Wait": {"Sync", "WaitGroup_Wait"},
	},
	"sync.Once": {
		"Do": {"Sync", "Once_Do"},
	},
	"sync.Map": {
		"Store": {"Sync", "Map_Store"}, "Load": {"Sync", "Map_Load"}, "Delete": {"Sync", "Map_Delete"},
	},
	"time.Time": {
		"Unix": {"Time", "Time_Unix"}, "UnixNano": {"Time", "Time_UnixNano"}, "UnixMilli": {"Time", "Time_UnixMilli"},
		"Year": {"Time", "Time_Year"}, "Month": {"Time", "Time_Month"}, "Day": {"Time", "Time_Day"},
		"Hour": {"Time", "Time_Hour"}, "Minute": {"Time", "Time_Minute"}, "Second": {"Time", "Time_Second"},
		"Nanosecond": {"Time", "Time_Nanosecond"}, "Weekday": {"Time", "Time_Weekday"},
		"Add": {"Time", "Time_Add"}, "Sub": {"Time", "Time_Sub"},
		"Before": {"Time", "Time_Before"}, "After": {"Time", "Time_After"}, "Equal": {"Time", "Time_Equal"},
		"IsZero": {"Time", "Time_IsZero"}, "UTC": {"Time", "Time_UTC"}, "Local": {"Time", "Time_Local"},
		"String": {"Time", "Time_String"}, "Format": {"Time", "Time_Format"},
	},
	"time.Duration": {
		"Seconds": {"Time", "Duration_Seconds"}, "Minutes": {"Time", "Duration_Minutes"},
		"Hours": {"Time", "Duration_Hours"}, "Nanoseconds": {"Time", "Duration_Nanoseconds"},
		"Microseconds": {"Time", "Duration_Microseconds"}, "Milliseconds": {"Time", "Duration_Milliseconds"},
		"String": {"Time", "Duration_String"},
	},
	"reflect.Value": {
		"Kind": {"Reflect", "Value_Kind"}, "Type": {"Reflect", "Value_Type"},
		"Interface": {"Reflect", "Value_Interface"}, "Int": {"Reflect", "Value_Int"},
		"Uint": {"Reflect", "Value_Uint"}, "Float": {"Reflect", "Value_Float"},
		"String": {"Reflect", "Value_String"}, "Bool": {"Reflect", "Value_Bool"},
		"Len": {"Reflect", "Value_Len"}, "Index": {"Reflect", "Value_Index"},
		"Field": {"Reflect", "Value_Field"}, "NumField": {"Reflect", "Value_NumField"},
		"IsNil": {"Reflect", "Value_IsNil"}, "IsZero": {"Reflect", "Value_IsZero"},
		"IsValid": {"Reflect", "Value_IsValid"}, "Elem": {"Reflect", "Value_Elem"},
		"MapKeys": {"Reflect", "Value_MapKeys"}, "MapIndex": {"Reflect", "Value_MapIndex"},
	},
}

// shimMethodExtern builds an extern descriptor for a method call on a shimmed
// stdlib type (e.g. reflect.Type.Kind), with the receiver as the first argument.
func (l *funcLowerer) shimMethodExtern(seln *types.Selection) (*goir.Extern, bool) {
	fn, ok := seln.Obj().(*types.Func)
	if !ok {
		return nil, false
	}
	recv := namedOf(seln.Recv())
	if recv == nil || recv.Obj() == nil || recv.Obj().Pkg() == nil {
		return nil, false
	}
	methods, ok := shimMethodRegistry[recv.Obj().Pkg().Path()+"."+recv.Obj().Name()]
	if !ok {
		return nil, false
	}
	sf, ok := methods[fn.Name()]
	if !ok {
		return nil, false
	}
	sig := fn.Type().(*types.Signature)
	// Receiver IR type: opaque handle (object) for reflect.Type/Value, or the
	// underlying type for named primitives (e.g. time.Duration -> i8).
	recvIR := goir.TObject
	if rt, ok := l.lowerCtx.goType(recv); ok {
		recvIR = rt
	}
	params := []goir.Type{recvIR}
	for i := 0; i < sig.Params().Len(); i++ {
		pt, ok := l.lowerCtx.goType(sig.Params().At(i).Type())
		if !ok {
			return nil, false
		}
		params = append(params, pt)
	}
	ret := goir.TVoid
	switch sig.Results().Len() {
	case 0:
	case 1:
		ret, _ = l.lowerCtx.goType(sig.Results().At(0).Type())
	default:
		ret = goir.TObjectArray
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Params: params, Ret: ret}, true
}

// shimMethodCall lowers a shimmed method call: receiver then args, OpCallExtern.
func (l *funcLowerer) shimMethodCall(e *ast.CallExpr, sel *ast.SelectorExpr, ext *goir.Extern) goir.Type {
	l.expr(sel.X) // receiver handle
	for i, a := range e.Args {
		if i+1 < len(ext.Params) {
			l.exprCoerced(a, ext.Params[i+1])
		} else {
			l.expr(a)
		}
	}
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return ext.Ret
}

// shimExtern builds an extern descriptor for a call to a shimmed stdlib function,
// deriving its parameter and result IR types from the Go signature. Returns false
// if the function is not shimmed (or has a multi-result signature, not yet
// supported).
func (l *funcLowerer) shimExtern(fn *types.Func) (*goir.Extern, bool) {
	pkg := fn.Pkg()
	if pkg == nil {
		return nil, false
	}
	funcs, ok := shimRegistry[pkg.Path()]
	if !ok {
		return nil, false
	}
	sf, ok := funcs[fn.Name()]
	if !ok {
		return nil, false
	}
	sig, ok := fn.Type().(*types.Signature)
	if !ok {
		return nil, false
	}
	var params []goir.Type
	for i := 0; i < sig.Params().Len(); i++ {
		pt, ok := l.lowerCtx.goType(sig.Params().At(i).Type())
		if !ok {
			return nil, false
		}
		params = append(params, pt)
	}
	ret := goir.TVoid
	switch sig.Results().Len() {
	case 0:
	case 1:
		ret, ok = l.lowerCtx.goType(sig.Results().At(0).Type())
		if !ok {
			return nil, false
		}
	default:
		// Multi-result (e.g. (int, error)): the shim returns a boxed object[]
		// tuple, which multiAssignCall unpacks per the Go result types.
		ret = goir.TObjectArray
	}
	return &goir.Extern{
		Assembly:  shimAssembly,
		Namespace: shimAssembly,
		Type:      sf.csType,
		Method:    sf.csMethod,
		Params:    params,
		Ret:       ret,
	}, true
}

// shimCall lowers a call to a shimmed stdlib function: arguments are lowered with
// emitCallArgs (which packs a variadic tail into a slice matching the shim's last
// parameter), then OpCallExtern.
func (l *funcLowerer) shimCall(e *ast.CallExpr, ext *goir.Extern, variadic bool) goir.Type {
	l.emitCallArgs(e.Args, ext.Params, variadic, e.Ellipsis.IsValid())
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return ext.Ret
}
