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
		"Inf":         {"Math", "Inf"},
		"Float64bits": {"Math", "Float64bits"}, "Float64frombits": {"Math", "Float64frombits"}, "Float32bits": {"Math", "Float32bits"}, "Float32frombits": {"Math", "Float32frombits"},
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
		"New": {"Reflect", "New"},
	},
	"encoding/json": {
		"Marshal": {"Json", "Marshal"},
	},
	"encoding/hex": {
		"EncodeToString": {"Hex", "EncodeToString"}, "DecodeString": {"Hex", "DecodeString"},
		"EncodedLen": {"Hex", "EncodedLen"}, "DecodedLen": {"Hex", "DecodedLen"},
	},
	"crypto/sha256":  {"New": {"Crypto", "Sha256New"}, "New224": {"Crypto", "Sha224New"}},
	"crypto/sha1":    {"New": {"Crypto", "Sha1New"}},
	"crypto/sha512":  {"New": {"Crypto", "Sha512New"}, "New384": {"Crypto", "Sha384New"}},
	"crypto/md5":     {"New": {"Crypto", "Md5New"}},
	"crypto/rand":    {"Read": {"Crypto", "RandRead"}},
	"crypto/hmac":    {"New": {"Crypto", "HmacNew"}, "Equal": {"Crypto", "HmacEqual"}},
	"crypto/subtle":  {"ConstantTimeCompare": {"Subtle", "ConstantTimeCompare"}, "ConstantTimeByteEq": {"Subtle", "ConstantTimeByteEq"}, "ConstantTimeEq": {"Subtle", "ConstantTimeEq"}, "ConstantTimeSelect": {"Subtle", "ConstantTimeSelect"}},
	"mime":           {"TypeByExtension": {"Mime", "TypeByExtension"}},
	"os/exec":        {"Command": {"Exec", "Command"}},
	"container/list": {"New": {"List", "New"}},
	"encoding/csv":   {"NewReader": {"Csv", "NewReader"}, "NewWriter": {"Csv", "NewWriter"}},
	"crypto/aes":     {"NewCipher": {"Aes", "NewCipher"}},
	"crypto/cipher":  {"NewGCM": {"Aes", "NewGCM"}},
	"compress/gzip":  {"NewWriter": {"Compress", "GzipNewWriter"}, "NewReader": {"Compress", "GzipNewReader"}},
	"compress/zlib":  {"NewWriter": {"Compress", "ZlibNewWriter"}, "NewReader": {"Compress", "ZlibNewReader"}},
	"compress/flate": {"NewWriter": {"Compress", "FlateNewWriter"}, "NewReader": {"Compress", "FlateNewReader"}},
	"net/url": {
		"QueryEscape": {"Url", "QueryEscape"}, "PathEscape": {"Url", "PathEscape"},
		"QueryUnescape": {"Url", "QueryUnescape"}, "PathUnescape": {"Url", "PathUnescape"},
		"Parse": {"Url", "Parse"},
	},
	"regexp": {
		"Compile": {"Regexp", "Compile"}, "MustCompile": {"Regexp", "MustCompile"},
		"MatchString": {"Regexp", "MatchString"}, "QuoteMeta": {"Regexp", "QuoteMeta"},
	},
	"log": {
		"Print": {"Log", "Print"}, "Println": {"Log", "Println"}, "Printf": {"Log", "Printf"},
		"Fatal": {"Log", "Fatal"}, "Fatalf": {"Log", "Fatalf"}, "Fatalln": {"Log", "Fatalln"},
		"Panic": {"Log", "Panic"}, "Panicf": {"Log", "Panicf"},
		"SetFlags": {"Log", "SetFlags"}, "SetPrefix": {"Log", "SetPrefix"}, "Flags": {"Log", "Flags"}, "Prefix": {"Log", "Prefix"},
	},
	"math/big": {
		"NewInt": {"Big", "NewInt"},
	},
	"path": {
		"Join": {"Path", "Join"}, "Base": {"Path", "Base"}, "Dir": {"Path", "Dir"},
		"Ext": {"Path", "Ext"}, "Clean": {"Path", "Clean"}, "Split": {"Path", "Split"}, "IsAbs": {"Path", "IsAbs"},
	},
	"path/filepath": {
		"Join": {"Path", "Join"}, "Base": {"Path", "Base"}, "Dir": {"Path", "Dir"},
		"Ext": {"Path", "Ext"}, "Clean": {"Path", "Clean"}, "Split": {"Path", "Split"}, "IsAbs": {"Path", "IsAbs"},
		"ToSlash": {"Path", "ToSlash"}, "FromSlash": {"Path", "FromSlash"},
	},
	"fmt": {
		"Sprint": {"Fmt", "Sprint"}, "Sprintln": {"Fmt", "Sprintln"}, "Sprintf": {"Fmt", "Sprintf"},
		"Print": {"Fmt", "Print"}, "Println": {"Fmt", "Println"}, "Printf": {"Fmt", "Printf"},
		"Errorf": {"Fmt", "Errorf"},
		"Fprint": {"Fmt", "Fprint"}, "Fprintln": {"Fmt", "Fprintln"}, "Fprintf": {"Fmt", "Fprintf"},
	},
	"io": {
		"WriteString": {"Io", "WriteString"}, "ReadAll": {"Readers", "ReadAll"}, "Copy": {"Readers", "Copy"},
	},
	"bufio": {
		"NewScanner": {"Bufio", "NewScanner"},
	},
	"net": {
		"Listen": {"Net", "Listen"}, "Dial": {"Net", "Dial"},
	},
	"net/http": {
		"Get": {"Http", "Get"}, "Post": {"Http", "Post"},
		"HandleFunc": {"Http", "HandleFunc"}, "ListenAndServe": {"Http", "ListenAndServe"},
	},
	"math/rand": {
		"NewSource": {"Rand", "NewSource"}, "New": {"Rand", "New"},
	},
	"sync/atomic": {
		"AddInt64": {"Atomic", "AddInt64"}, "AddInt32": {"Atomic", "AddInt32"}, "AddUint64": {"Atomic", "AddUint64"},
		"LoadInt64": {"Atomic", "LoadInt64"}, "LoadInt32": {"Atomic", "LoadInt32"}, "LoadUint64": {"Atomic", "LoadUint64"},
		"StoreInt64": {"Atomic", "StoreInt64"}, "StoreInt32": {"Atomic", "StoreInt32"}, "StoreUint64": {"Atomic", "StoreUint64"},
		"SwapInt64": {"Atomic", "SwapInt64"}, "SwapInt32": {"Atomic", "SwapInt32"},
		"CompareAndSwapInt64": {"Atomic", "CompareAndSwapInt64"}, "CompareAndSwapInt32": {"Atomic", "CompareAndSwapInt32"},
	},
	"context": {
		"Background": {"Context", "Background"}, "TODO": {"Context", "TODO"},
		"WithValue": {"Context", "WithValue"}, "WithCancel": {"Context", "WithCancel"},
		"WithTimeout": {"Context", "WithTimeout"},
	},
	"sort": {
		"Ints": {"Sort", "Ints"}, "Float64s": {"Sort", "Float64s"}, "Strings": {"Sort", "Strings"},
		"IntsAreSorted": {"Sort", "IntsAreSorted"}, "SearchInts": {"Sort", "SearchInts"},
		"Float64sAreSorted": {"Sort", "Float64sAreSorted"}, "StringsAreSorted": {"Sort", "StringsAreSorted"},
		"SearchStrings": {"Sort", "SearchStrings"}, "SearchFloat64s": {"Sort", "SearchFloat64s"},
		"Search": {"Sort", "Search"}, "Slice": {"Sort", "Slice"},
	},
	"time": {
		"Sleep": {"Time", "Sleep"},
		"Now":   {"Time", "Now"}, "Unix": {"Time", "Unix"}, "Date": {"Time", "Date"}, "Since": {"Time", "Since"},
	},
	"math/bits": {
		"OnesCount": {"MathBits", "OnesCount"}, "OnesCount64": {"MathBits", "OnesCount64"}, "OnesCount32": {"MathBits", "OnesCount32"},
		"LeadingZeros": {"MathBits", "LeadingZeros"}, "LeadingZeros64": {"MathBits", "LeadingZeros64"},
		"TrailingZeros": {"MathBits", "TrailingZeros"}, "TrailingZeros64": {"MathBits", "TrailingZeros64"},
		"Len": {"MathBits", "Len"}, "Len64": {"MathBits", "Len64"}, "RotateLeft64": {"MathBits", "RotateLeft64"},
		"Reverse64": {"MathBits", "Reverse64"}, "ReverseBytes64": {"MathBits", "ReverseBytes64"},
		"OnesCount8": {"MathBits", "OnesCount8"}, "OnesCount16": {"MathBits", "OnesCount16"},
		"LeadingZeros8": {"MathBits", "LeadingZeros8"}, "LeadingZeros16": {"MathBits", "LeadingZeros16"}, "LeadingZeros32": {"MathBits", "LeadingZeros32"},
		"TrailingZeros8": {"MathBits", "TrailingZeros8"}, "TrailingZeros16": {"MathBits", "TrailingZeros16"}, "TrailingZeros32": {"MathBits", "TrailingZeros32"},
		"Len8": {"MathBits", "Len8"}, "Len16": {"MathBits", "Len16"}, "Len32": {"MathBits", "Len32"},
		"RotateLeft8": {"MathBits", "RotateLeft8"}, "RotateLeft16": {"MathBits", "RotateLeft16"}, "RotateLeft32": {"MathBits", "RotateLeft32"},
		"ReverseBytes16": {"MathBits", "ReverseBytes16"}, "ReverseBytes32": {"MathBits", "ReverseBytes32"}, "Reverse32": {"MathBits", "Reverse32"},
	},
	"os": {
		"Getenv": {"Os", "Getenv"}, "LookupEnv": {"Os", "LookupEnv"}, "Setenv": {"Os", "Setenv"},
		"Unsetenv": {"Os", "Unsetenv"}, "Exit": {"Os", "Exit"}, "Getpid": {"Os", "Getpid"},
	},
	"bytes": {
		"Equal": {"Bytes", "Equal"}, "Compare": {"Bytes", "Compare"}, "Contains": {"Bytes", "Contains"},
		"HasPrefix": {"Bytes", "HasPrefix"}, "HasSuffix": {"Bytes", "HasSuffix"}, "Index": {"Bytes", "Index"},
		"LastIndex": {"Bytes", "LastIndex"}, "LastIndexByte": {"Bytes", "LastIndexByte"},
		"IndexByte": {"Bytes", "IndexByte"}, "Count": {"Bytes", "Count"}, "ToUpper": {"Bytes", "ToUpper"},
		"ToLower": {"Bytes", "ToLower"}, "TrimSpace": {"Bytes", "TrimSpace"}, "Repeat": {"Bytes", "Repeat"},
		"Split": {"Bytes", "Split"}, "Join": {"Bytes", "Join"},
		"NewReader": {"Readers", "NewBytesReader"}, "NewBuffer": {"BytesBuffer", "NewBuffer"}, "NewBufferString": {"BytesBuffer", "NewBufferString"},
	},
	"strconv": {
		"Itoa": {"Strconv", "Itoa"}, "Atoi": {"Strconv", "Atoi"},
		"FormatInt": {"Strconv", "FormatInt"}, "FormatUint": {"Strconv", "FormatUint"},
		"FormatBool": {"Strconv", "FormatBool"}, "FormatFloat": {"Strconv", "FormatFloat"},
		"ParseInt": {"Strconv", "ParseInt"}, "ParseUint": {"Strconv", "ParseUint"},
		"ParseFloat": {"Strconv", "ParseFloat"}, "ParseBool": {"Strconv", "ParseBool"},
		"Quote": {"Strconv", "Quote"}, "QuoteToASCII": {"Strconv", "QuoteToASCII"},
	},
	"unicode/utf8": {
		"RuneCountInString": {"Utf8", "RuneCountInString"}, "RuneCount": {"Utf8", "RuneCount"},
		"ValidString": {"Utf8", "ValidString"}, "ValidRune": {"Utf8", "ValidRune"}, "RuneLen": {"Utf8", "RuneLen"},
		"Valid": {"Utf8", "Valid"}, "EncodeRune": {"Utf8", "EncodeRune"},
		"DecodeRuneInString": {"Utf8", "DecodeRuneInString"}, "DecodeRune": {"Utf8", "DecodeRune"},
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
		"Join": {"Strings", "Join"}, "NewReader": {"Readers", "NewStringReader"}, "Cut": {"Strings", "Cut"}, "IndexRune": {"Strings", "IndexRune"},
		"ContainsRune": {"Strings", "ContainsRune"}, "ContainsAny": {"Strings", "ContainsAny"},
		"IndexAny": {"Strings", "IndexAny"}, "LastIndexByte": {"Strings", "LastIndexByte"},
		"ToTitle": {"Strings", "ToTitle"}, "SplitAfter": {"Strings", "SplitAfter"}, "Map": {"Strings", "Map"},
		"TrimFunc": {"Strings", "TrimFunc"}, "TrimLeftFunc": {"Strings", "TrimLeftFunc"}, "TrimRightFunc": {"Strings", "TrimRightFunc"},
		"IndexFunc": {"Strings", "IndexFunc"}, "FieldsFunc": {"Strings", "FieldsFunc"},
	},
}

// opaqueShimTypes are stdlib types represented at runtime as opaque object
// handles (not lowered structures); method calls on them dispatch to shims.
var opaqueShimTypes = map[string]bool{
	"reflect.Type":                 true,
	"reflect.Value":                true,
	"sync.Mutex":                   true,
	"sync.RWMutex":                 true,
	"sync.WaitGroup":               true,
	"sync.Once":                    true,
	"sync.Map":                     true,
	"strings.Builder":              true,
	"bytes.Buffer":                 true,
	"os.File":                      true,
	"time.Time":                    true,
	"time.Location":                true,
	"math/rand.Rand":               true,
	"math/rand.Source":             true,
	"encoding/base64.Encoding":     true,
	"encoding/binary.littleEndian": true,
	"encoding/binary.bigEndian":    true,
	"encoding/binary.ByteOrder":    true,
	"regexp.Regexp":                true,
	"net/url.URL":                  true,
	"net/http.Response":            true,
	"net.Listener":                 true,
	"net.Conn":                     true,
	"net.PacketConn":               true,
	"net/http.ResponseWriter":      true,
	"net/http.Request":             true,
	"os/exec.Cmd":                  true,
	"container/list.List":          true,
	"container/list.Element":       true,
	"encoding/csv.Reader":          true,
	"encoding/csv.Writer":          true,
	"compress/gzip.Writer":         true,
	"compress/gzip.Reader":         true,
	"compress/zlib.Writer":         true,
	"compress/flate.Writer":        true,
	"crypto/cipher.Block":          true,
	"crypto/cipher.AEAD":           true,
	"math/big.Int":                 true,
	"encoding/base32.Encoding":     true,
	"strings.Reader":               true,
	"bytes.Reader":                 true,
	"bufio.Scanner":                true,
}

// shimVarRegistry maps "importpath.VarName" stdlib package variables to a
// no-argument accessor returning the runtime object.
var shimVarRegistry = map[string]shimFunc{
	"os.Stdout":                      {"Os", "Stdout"},
	"os.Stderr":                      {"Os", "Stderr"},
	"os.Stdin":                       {"Os", "Stdin"},
	"time.UTC":                       {"Time", "UTC"},
	"time.Local":                     {"Time", "Local"},
	"encoding/base64.StdEncoding":    {"Base64", "StdEncoding"},
	"encoding/base64.URLEncoding":    {"Base64", "URLEncoding"},
	"encoding/base64.RawStdEncoding": {"Base64", "RawStdEncoding"},
	"encoding/base64.RawURLEncoding": {"Base64", "RawURLEncoding"},
	"encoding/binary.LittleEndian":   {"Binary", "LittleEndian"},
	"encoding/binary.BigEndian":      {"Binary", "BigEndian"},
	"encoding/base32.StdEncoding":    {"Base32", "StdEncoding"},
	"context.Canceled":               {"Context", "Canceled"},
	"context.DeadlineExceeded":       {"Context", "DeadlineExceeded"},
}

// shimFuncValue, when e is a reference to a shimmed stdlib function used as a
// value (not a call), emits a native GoClosure wrapping that shim (invoked by
// reflection at runtime), and reports true.
func (l *funcLowerer) shimFuncValue(e ast.Expr) bool {
	var id *ast.Ident
	switch x := e.(type) {
	case *ast.Ident:
		id = x
	case *ast.SelectorExpr:
		id = x.Sel
	default:
		return false
	}
	fn, ok := l.pkg.TypesInfo.Uses[id].(*types.Func)
	if !ok || fn.Pkg() == nil {
		return false
	}
	funcs, ok := shimRegistry[fn.Pkg().Path()]
	if !ok {
		return false
	}
	sf, ok := funcs[fn.Name()]
	if !ok {
		return false
	}
	// The resulting native closure may be invoked through the dispatcher, so
	// ensure it exists and is registered at startup.
	l.needsInvoker = true
	l.invokeMethod()
	l.emit(goir.Op{Code: goir.OpStrConst, Str: sf.csType})
	l.emit(goir.Op{Code: goir.OpStrConst, Str: sf.csMethod})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "NativeClosures", Method: "FromShim",
		Params: []goir.Type{goir.TString, goir.TString}, Ret: goir.TFunc,
	}})
	return true
}

// shimFieldRegistry maps "importpath.Type" to its readable fields, each lowering
// to a getter (the C# method takes the opaque object as its only argument).
var shimFieldRegistry = map[string]map[string]shimFunc{
	"net/url.URL": {
		"Scheme": {"Url", "URL_Scheme"}, "Host": {"Url", "URL_Host"}, "Path": {"Url", "URL_Path"},
		"RawQuery": {"Url", "URL_RawQuery"}, "Fragment": {"Url", "URL_Fragment"},
		"User": {"Url", "URL_User"}, "RawPath": {"Url", "URL_Path"}, "Opaque": {"Url", "URL_Opaque"},
	},
	"net/http.Response": {
		"StatusCode": {"Http", "Resp_StatusCode"}, "Status": {"Http", "Resp_Status"},
		"Body": {"Http", "Resp_Body"}, "ContentLength": {"Http", "Resp_ContentLength"},
	},
	"container/list.Element": {
		"Value": {"List", "Element_Value"},
	},
	"net/http.Request": {
		"Method": {"Http", "Req_Method"}, "URL": {"Http", "Req_URL"}, "Body": {"Http", "Req_Body"},
		"Host": {"Http", "Req_Host"}, "RemoteAddr": {"Http", "Req_RemoteAddr"},
	},
}

// shimFieldExtern returns the getter extern for a shim type's readable field.
func shimFieldExtern(shim, field string, ret goir.Type) (*goir.Extern, bool) {
	fm, ok := shimFieldRegistry[shim]
	if !ok {
		return nil, false
	}
	sf, ok := fm[field]
	if !ok {
		return nil, false
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Params: []goir.Type{goir.TObject}, Ret: ret}, true
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
	"math/big.Int":    {"Big", "IntZero"},
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

// binaryMethods are the encoding/binary.ByteOrder methods (shared by the
// little-/big-endian concrete types and the interface).
var binaryMethods = map[string]shimFunc{
	"Uint16": {"Binary", "Uint16"}, "Uint32": {"Binary", "Uint32"}, "Uint64": {"Binary", "Uint64"},
	"PutUint16": {"Binary", "PutUint16"}, "PutUint32": {"Binary", "PutUint32"}, "PutUint64": {"Binary", "PutUint64"},
}

var shimMethodRegistry = map[string]map[string]shimFunc{
	"reflect.Type": {
		"Kind": {"Reflect", "Type_Kind"}, "Name": {"Reflect", "Type_Name"},
		"String": {"Reflect", "Type_String"}, "NumField": {"Reflect", "Type_NumField"},
		"Elem": {"Reflect", "Type_Elem"},
	},
	"reflect.Kind": {
		"String": {"Reflect", "Kind_String"},
	},
	"bufio.Scanner": {
		"Scan": {"Bufio", "Scanner_Scan"}, "Text": {"Bufio", "Scanner_Text"}, "Bytes": {"Bufio", "Scanner_Bytes"},
	},
	"io.ReadCloser": {
		"Close": {"Http", "Body_Close"},
	},
	"net/http.ResponseWriter": {
		"Write": {"Http", "RW_Write"}, "WriteHeader": {"Http", "RW_WriteHeader"},
	},
	"net.Listener": {
		"Accept": {"Net", "Listener_Accept"}, "Close": {"Net", "Listener_Close"},
	},
	"net.Conn": {
		"Read": {"Net", "Conn_Read"}, "Write": {"Net", "Conn_Write"}, "Close": {"Net", "Conn_Close"},
	},
	"os/exec.Cmd": {
		"Output": {"Exec", "Cmd_Output"}, "CombinedOutput": {"Exec", "Cmd_CombinedOutput"}, "Run": {"Exec", "Cmd_Run"},
	},
	"container/list.List": {
		"Len": {"List", "List_Len"}, "Front": {"List", "List_Front"}, "Back": {"List", "List_Back"},
		"PushBack": {"List", "List_PushBack"}, "PushFront": {"List", "List_PushFront"}, "Remove": {"List", "List_Remove"},
	},
	"container/list.Element": {
		"Next": {"List", "Element_Next"}, "Prev": {"List", "Element_Prev"},
	},
	"encoding/csv.Reader": {
		"ReadAll": {"Csv", "ReadAll"},
	},
	"encoding/csv.Writer": {
		"Write": {"Csv", "Write"}, "Flush": {"Csv", "Flush"},
	},
	"compress/gzip.Writer": {
		"Write": {"Compress", "CompW_Write"}, "Close": {"Compress", "CompW_Close"}, "Flush": {"Compress", "CompW_Flush"},
	},
	"compress/zlib.Writer": {
		"Write": {"Compress", "CompW_Write"}, "Close": {"Compress", "CompW_Close"}, "Flush": {"Compress", "CompW_Flush"},
	},
	"compress/flate.Writer": {
		"Write": {"Compress", "CompW_Write"}, "Close": {"Compress", "CompW_Close"}, "Flush": {"Compress", "CompW_Flush"},
	},
	"crypto/cipher.AEAD": {
		"Seal": {"Aes", "GCM_Seal"}, "Open": {"Aes", "GCM_Open"}, "NonceSize": {"Aes", "GCM_NonceSize"}, "Overhead": {"Aes", "GCM_Overhead"},
	},
	"encoding/base64.Encoding": {
		"EncodeToString": {"Base64", "EncodeToString"}, "DecodeString": {"Base64", "DecodeString"},
	},
	"encoding/binary.littleEndian": binaryMethods,
	"encoding/binary.bigEndian":    binaryMethods,
	"encoding/binary.ByteOrder":    binaryMethods,
	"hash.Hash": {
		"Write": {"Crypto", "Hash_Write"}, "Sum": {"Crypto", "Hash_Sum"}, "Reset": {"Crypto", "Hash_Reset"},
		"Size": {"Crypto", "Hash_Size"}, "BlockSize": {"Crypto", "Hash_BlockSize"},
	},
	"regexp.Regexp": {
		"MatchString": {"Regexp", "Re_MatchString"}, "FindString": {"Regexp", "Re_FindString"},
		"FindStringSubmatch": {"Regexp", "Re_FindStringSubmatch"}, "FindAllString": {"Regexp", "Re_FindAllString"},
		"ReplaceAllString": {"Regexp", "Re_ReplaceAllString"}, "Split": {"Regexp", "Re_Split"},
		"String": {"Regexp", "Re_String"},
	},
	"encoding/base32.Encoding": {
		"EncodeToString": {"Base32", "EncodeToString"}, "DecodeString": {"Base32", "DecodeString"},
	},
	"math/big.Int": {
		"Add": {"Big", "Int_Add"}, "Sub": {"Big", "Int_Sub"}, "Mul": {"Big", "Int_Mul"},
		"Div": {"Big", "Int_Div"}, "Mod": {"Big", "Int_Mod"}, "Neg": {"Big", "Int_Neg"},
		"Quo": {"Big", "Int_Quo"}, "Rem": {"Big", "Int_Rem"}, "GCD": {"Big", "Int_GCD"},
		"Abs": {"Big", "Int_Abs"}, "Exp": {"Big", "Int_Exp"}, "Set": {"Big", "Int_Set"},
		"Cmp": {"Big", "Int_Cmp"}, "Sign": {"Big", "Int_Sign"}, "Int64": {"Big", "Int_Int64"},
		"String": {"Big", "Int_String"}, "SetString": {"Big", "Int_SetString"},
	},
	"strings.Builder": {
		"WriteString": {"StringsBuilder", "WriteString"}, "WriteByte": {"StringsBuilder", "WriteByte"},
		"WriteRune": {"StringsBuilder", "WriteRune"}, "Write": {"StringsBuilder", "Write"},
		"String": {"StringsBuilder", "String"}, "Len": {"StringsBuilder", "Len"},
		"Cap": {"StringsBuilder", "Cap"}, "Reset": {"StringsBuilder", "Reset"}, "Grow": {"StringsBuilder", "Grow"},
	},
	"bytes.Buffer": {
		"WriteString": {"BytesBuffer", "WriteString"}, "WriteByte": {"BytesBuffer", "WriteByte"},
		"WriteRune": {"BytesBuffer", "WriteRune"},
		"Write":     {"BytesBuffer", "Write"}, "String": {"BytesBuffer", "String"},
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
		"LoadOrStore": {"Sync", "Map_LoadOrStore"}, "LoadAndDelete": {"Sync", "Map_LoadAndDelete"},
	},
	"math/rand.Rand": {
		"Int63": {"Rand", "Rand_Int63"}, "Int": {"Rand", "Rand_Int"}, "Int63n": {"Rand", "Rand_Int63n"},
		"Intn": {"Rand", "Rand_Intn"}, "Float64": {"Rand", "Rand_Float64"}, "Perm": {"Rand", "Rand_Perm"},
	},
	"context.Context": {
		"Value": {"Context", "Context_Value"}, "Err": {"Context", "Context_Err"},
		"Done": {"Context", "Context_Done"},
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
	"time.Month": {
		"String": {"Time", "Month_String"},
	},
	"time.Weekday": {
		"String": {"Time", "Weekday_String"},
	},
	"time.Duration": {
		"Seconds": {"Time", "Duration_Seconds"}, "Minutes": {"Time", "Duration_Minutes"},
		"Hours": {"Time", "Duration_Hours"}, "Nanoseconds": {"Time", "Duration_Nanoseconds"},
		"Microseconds": {"Time", "Duration_Microseconds"}, "Milliseconds": {"Time", "Duration_Milliseconds"},
		"String": {"Time", "Duration_String"}, "Truncate": {"Time", "Duration_Truncate"}, "Round": {"Time", "Duration_Round"},
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
		"CanSet": {"Reflect", "Value_CanSet"}, "CanAddr": {"Reflect", "Value_CanAddr"},
		"SetInt": {"Reflect", "Value_SetInt"}, "SetUint": {"Reflect", "Value_SetUint"},
		"SetFloat": {"Reflect", "Value_SetFloat"}, "SetBool": {"Reflect", "Value_SetBool"},
		"SetString": {"Reflect", "Value_SetString"}, "Set": {"Reflect", "Value_Set"},
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
