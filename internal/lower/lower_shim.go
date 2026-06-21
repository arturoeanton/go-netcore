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
		"Acosh": {"Math", "Acosh"}, "Asinh": {"Math", "Asinh"}, "Atanh": {"Math", "Atanh"},
		"Expm1": {"Math", "Expm1"}, "Log1p": {"Math", "Log1p"},
	},
	"go/ast": {
		"IsExported": {"Ast", "IsExported"},
	},
	"runtime": {
		"FuncForPC": {"Goruntime", "FuncForPC"}, "GOMAXPROCS": {"Goruntime", "GOMAXPROCS"}, "Caller": {"Goruntime", "Caller"}, "Stack": {"Goruntime", "Stack"},
		"NumCPU": {"Goruntime", "NumCPU"}, "NumGoroutine": {"Goruntime", "NumGoroutine"},
		"GC": {"Goruntime", "GC"}, "Gosched": {"Goruntime", "Gosched"}, "Version": {"Goruntime", "Version"},
	},
	"flag": {
		"Lookup": {"Flag", "Lookup"},
	},
	"syscall": {
		"FcntlFlock": {"Syscall", "FcntlFlock"}, "Fsync": {"Syscall", "Fsync"},
	},
	"encoding/xml": {
		"Marshal": {"Xml", "Marshal"}, "MarshalIndent": {"Xml", "MarshalIndent"}, "NewEncoder": {"Xml", "NewEncoder"},
		"Unmarshal": {"Xml", "Unmarshal"}, "NewDecoder": {"Xml", "NewDecoder"},
	},
	"net/http/httputil": {
		"DumpRequest": {"Httputil", "DumpRequest"},
	},
	"errors": {
		"New": {"Errors", "New"}, "Unwrap": {"Errors", "Unwrap"}, "Is": {"Errors", "Is"},
	},
	// NOTE: "unicode" is compiled from real Go source (see compileFromSource in
	// lower.go), not shimmed — it provides RangeTable/Is/In and the full tables.
	"reflect": {
		"TypeOf": {"Reflect", "TypeOf"}, "ValueOf": {"Reflect", "ValueOf"}, "DeepEqual": {"Reflect", "DeepEqual"}, "MakeSlice": {"Reflect", "MakeSlice"}, "MakeMap": {"Reflect", "MakeMap"}, "Zero": {"Reflect", "Zero"},
		"New": {"Reflect", "New"}, "PointerTo": {"Reflect", "PointerTo"}, "PtrTo": {"Reflect", "PointerTo"},
		"MapOf": {"Reflect", "MapOf"}, "SliceOf": {"Reflect", "SliceOf"}, "ArrayOf": {"Reflect", "ArrayOf"},
		"MakeFunc": {"Reflect", "MakeFunc"}, "Copy": {"Reflect", "Copy"}, "Indirect": {"Reflect", "Indirect"},
		"Append": {"Reflect", "Append"},
	},
	"encoding/json": {
		"Marshal": {"Json", "Marshal"}, "MarshalIndent": {"Json", "MarshalIndent"},
		"NewDecoder": {"Json", "NewDecoder"}, "NewEncoder": {"Json", "NewEncoder"},
		"Valid": {"Json", "Valid"}, "Unmarshal": {"Json", "UnmarshalValue"},
	},
	"encoding/hex": {
		"EncodeToString": {"Hex", "EncodeToString"}, "DecodeString": {"Hex", "DecodeString"},
		"EncodedLen": {"Hex", "EncodedLen"}, "DecodedLen": {"Hex", "DecodedLen"},
	},
	"crypto/sha256": {"New": {"Crypto", "Sha256New"}, "New224": {"Crypto", "Sha224New"}},
	"crypto/sha1":   {"New": {"Crypto", "Sha1New"}},
	"crypto/sha512": {"New": {"Crypto", "Sha512New"}, "New384": {"Crypto", "Sha384New"}},
	"crypto/sha3": {
		"New224": {"Crypto", "Sha3_224New"}, "New256": {"Crypto", "Sha3_256New"}, "New384": {"Crypto", "Sha3_384New"}, "New512": {"Crypto", "Sha3_512New"},
		"Sum224": {"Crypto", "Sha3Sum224"}, "Sum256": {"Crypto", "Sha3Sum256"}, "Sum384": {"Crypto", "Sha3Sum384"}, "Sum512": {"Crypto", "Sha3Sum512"},
		"NewSHAKE128": {"Crypto", "NewSHAKE128"}, "NewSHAKE256": {"Crypto", "NewSHAKE256"}, "NewCSHAKE128": {"Crypto", "NewCSHAKE128"}, "NewCSHAKE256": {"Crypto", "NewCSHAKE256"},
	},
	"crypto/md5":      {"New": {"Crypto", "Md5New"}},
	"crypto/rand":     {"Read": {"Crypto", "RandRead"}},
	"crypto/hmac":     {"New": {"Crypto", "HmacNew"}, "Equal": {"Crypto", "HmacEqual"}},
	"crypto/subtle":   {"ConstantTimeCompare": {"Subtle", "ConstantTimeCompare"}, "ConstantTimeByteEq": {"Subtle", "ConstantTimeByteEq"}, "ConstantTimeEq": {"Subtle", "ConstantTimeEq"}, "ConstantTimeSelect": {"Subtle", "ConstantTimeSelect"}, "XORBytes": {"Subtle", "XORBytes"}},
	"mime":            {"TypeByExtension": {"Mime", "TypeByExtension"}, "ParseMediaType": {"Mime", "ParseMediaType"}},
	"net/mail":        {"ParseAddress": {"Mail", "ParseAddress"}},
	"net/textproto":   {"CanonicalMIMEHeaderKey": {"Textproto", "CanonicalMIMEHeaderKey"}, "TrimString": {"Textproto", "TrimString"}, "TrimBytes": {"Textproto", "TrimBytes"}},
	"html/template":   {"New": {"Template", "New"}, "Must": {"Template", "Must"}, "JSEscapeString": {"Template", "JSEscapeString"}},
	"text/template":   {"New": {"Template", "New"}, "Must": {"Template", "Must"}},
	"os/exec":         {"Command": {"Exec", "Command"}},
	"container/list":  {"New": {"List", "New"}},
	"encoding/csv":    {"NewReader": {"Csv", "NewReader"}, "NewWriter": {"Csv", "NewWriter"}},
	"encoding/binary": {"Write": {"Binary", "Write"}, "Read": {"Binary", "Read"}, "Size": {"Binary", "Size"}, "PutUvarint": {"Binary", "PutUvarint"}, "Uvarint": {"Binary", "Uvarint"}, "PutVarint": {"Binary", "PutVarint"}, "Varint": {"Binary", "Varint"}},
	"crypto/aes":      {"NewCipher": {"Aes", "NewCipher"}},
	"crypto/cipher":   {"NewGCM": {"Aes", "NewGCM"}},
	"hash/fnv":        {"New32": {"Hashes", "Fnv32"}, "New32a": {"Hashes", "Fnv32a"}, "New64": {"Hashes", "Fnv64"}, "New64a": {"Hashes", "Fnv64a"}},
	"hash/crc32":      {"ChecksumIEEE": {"Hashes", "Crc32ChecksumIEEE"}},
	"hash/adler32":    {"Checksum": {"Hashes", "Adler32Checksum"}},
	"compress/gzip":   {"NewWriter": {"Compress", "GzipNewWriter"}, "NewReader": {"Compress", "GzipNewReader"}},
	"compress/zlib":   {"NewWriter": {"Compress", "ZlibNewWriter"}, "NewReader": {"Compress", "ZlibNewReader"}},
	"compress/flate":  {"NewWriter": {"Compress", "FlateNewWriter"}, "NewReader": {"Compress", "FlateNewReader"}},
	"net/url": {
		"QueryEscape": {"Url", "QueryEscape"}, "PathEscape": {"Url", "PathEscape"},
		"QueryUnescape": {"Url", "QueryUnescape"}, "PathUnescape": {"Url", "PathUnescape"},
		"Parse": {"Url", "Parse"}, "ParseRequestURI": {"Url", "ParseRequestURI"},
	},
	"regexp": {
		"Compile": {"Regexp", "Compile"}, "MustCompile": {"Regexp", "MustCompile"},
		"MatchString": {"Regexp", "MatchString"}, "QuoteMeta": {"Regexp", "QuoteMeta"},
	},
	"log": {
		"New": {"Log", "New"}, "Print": {"Log", "Print"}, "Println": {"Log", "Println"}, "Printf": {"Log", "Printf"},
		"Fatal": {"Log", "Fatal"}, "Fatalf": {"Log", "Fatalf"}, "Fatalln": {"Log", "Fatalln"},
		"Panic": {"Log", "Panic"}, "Panicf": {"Log", "Panicf"},
		"SetFlags": {"Log", "SetFlags"}, "SetPrefix": {"Log", "SetPrefix"}, "Flags": {"Log", "Flags"}, "Prefix": {"Log", "Prefix"},
	},
	"math/big": {
		"NewInt": {"Big", "NewInt"}, "NewFloat": {"Big", "NewFloat"},
	},
	"path": {
		"Join": {"Path", "Join"}, "Base": {"Path", "Base"}, "Dir": {"Path", "Dir"},
		"Ext": {"Path", "Ext"}, "Clean": {"Path", "Clean"}, "Split": {"Path", "Split"}, "IsAbs": {"Path", "IsAbs"},
	},
	"path/filepath": {
		"Join": {"Path", "Join"}, "Base": {"Path", "Base"}, "Dir": {"Path", "Dir"},
		"Ext": {"Path", "Ext"}, "Clean": {"Path", "Clean"}, "Split": {"Path", "Split"}, "IsAbs": {"Path", "IsAbs"},
		"ToSlash": {"Path", "ToSlash"}, "FromSlash": {"Path", "FromSlash"}, "Walk": {"Path", "Walk"}, "Abs": {"Path", "Abs"},
	},
	"fmt": {
		"Sprint": {"Fmt", "Sprint"}, "Sprintln": {"Fmt", "Sprintln"}, "Sprintf": {"Fmt", "Sprintf"},
		"Print": {"Fmt", "Print"}, "Println": {"Fmt", "Println"}, "Printf": {"Fmt", "Printf"},
		"Errorf": {"Fmt", "Errorf"},
		"Fprint": {"Fmt", "Fprint"}, "Fprintln": {"Fmt", "Fprintln"}, "Fprintf": {"Fmt", "Fprintf"},
	},
	"io": {
		"WriteString": {"Io", "WriteString"}, "ReadAll": {"Readers", "ReadAll"}, "Copy": {"Readers", "Copy"},
		"ReadFull": {"Io", "ReadFull"}, "NopCloser": {"Io", "NopCloser"},
	},
	"bufio": {
		"NewScanner": {"Bufio", "NewScanner"}, "NewWriter": {"Bufio", "NewWriter"}, "NewWriterSize": {"Bufio", "NewWriterSize"},
		"NewReader": {"Bufio", "NewReader"}, "NewReaderSize": {"Bufio", "NewReaderSize"},
	},
	"net": {
		"Listen": {"Net", "Listen"}, "Dial": {"Net", "Dial"}, "FileListener": {"Net", "FileListener"},
		"ParseIP": {"Net", "ParseIP"}, "ParseMAC": {"Net", "ParseMAC"}, "ParseCIDR": {"Net", "ParseCIDR"},
		"SplitHostPort": {"Net", "SplitHostPort"}, "JoinHostPort": {"Net", "JoinHostPort"},
		"ResolveTCPAddr": {"Net", "ResolveTCPAddr"}, "ResolveUDPAddr": {"Net", "ResolveUDPAddr"},
		"ResolveIPAddr": {"Net", "ResolveIPAddr"}, "ResolveUnixAddr": {"Net", "ResolveUnixAddr"},
	},
	"net/http": {
		"Get": {"Http", "Get"}, "Post": {"Http", "Post"},
		"HandleFunc": {"Http", "HandleFunc"}, "ListenAndServe": {"Http", "ListenAndServe"}, "Redirect": {"Http", "Redirect"}, "NewServeMux": {"Http", "NewServeMux"},
		"CanonicalHeaderKey": {"Http", "CanonicalHeaderKey"}, "StatusText": {"Http", "StatusText"}, "DetectContentType": {"Http", "DetectContentType"}, "Error": {"Http", "Error"},
		"NewResponseController": {"Http", "NewResponseController"}, "SetCookie": {"Http", "SetCookie"},
		"ServeFile": {"Http", "ServeFile"}, "FileServer": {"Http", "FileServer"}, "StripPrefix": {"Http", "StripPrefix"}, "Serve": {"Http", "Serve"}, "ListenAndServeTLS": {"Http", "ListenAndServeTLS"},
	},
	"math/rand/v2": {
		"IntN": {"Rand2", "IntN"}, "Int64N": {"Rand2", "Int64N"}, "Int32N": {"Rand2", "Int32N"}, "UintN": {"Rand2", "UintN"},
		"Int": {"Rand2", "Int"}, "Int64": {"Rand2", "Int64"}, "Int32": {"Rand2", "Int32"}, "Uint64": {"Rand2", "Uint64"}, "Uint32": {"Rand2", "Uint32"},
		"Float64": {"Rand2", "Float64"}, "Float32": {"Rand2", "Float32"}, "Shuffle": {"Rand2", "Shuffle"}, "Perm": {"Rand2", "Perm"},
	},
	"math/rand": {
		"NewSource": {"Rand", "NewSource"}, "New": {"Rand", "New"},
		"Float64": {"Rand", "Float64"}, "Int63": {"Rand", "Int63"}, "Int": {"Rand", "Int"},
		"Int63n": {"Rand", "Int63n"}, "Intn": {"Rand", "Intn"}, "Perm": {"Rand", "Perm"}, "Seed": {"Rand", "Seed"},
		"Uint64": {"Rand", "Uint64"}, "Uint32": {"Rand", "Uint32"}, "Int31": {"Rand", "Int31"}, "Read": {"Rand", "Read"},
	},
	"sync": {"NewCond": {"Sync", "NewCond"}},
	"sync/atomic": {
		"AddInt64": {"Atomic", "AddInt64"}, "AddInt32": {"Atomic", "AddInt32"}, "AddUint64": {"Atomic", "AddUint64"},
		"LoadInt64": {"Atomic", "LoadInt64"}, "LoadInt32": {"Atomic", "LoadInt32"}, "LoadUint64": {"Atomic", "LoadUint64"},
		"StoreInt64": {"Atomic", "StoreInt64"}, "StoreInt32": {"Atomic", "StoreInt32"}, "StoreUint64": {"Atomic", "StoreUint64"},
		"LoadUint32": {"Atomic", "LoadUint32"}, "StoreUint32": {"Atomic", "StoreUint32"},
		"AddUint32": {"Atomic", "AddUint32"}, "SwapUint64": {"Atomic", "SwapUint64"},
		"CompareAndSwapUint64": {"Atomic", "CompareAndSwapUint64"}, "CompareAndSwapUint32": {"Atomic", "CompareAndSwapUint32"},
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
		"Search": {"Sort", "Search"}, "Slice": {"Sort", "Slice"}, "SliceStable": {"Sort", "SliceStable"}, "SliceIsSorted": {"Sort", "SliceIsSorted"},
	},
	"time": {
		"Sleep": {"Time", "Sleep"}, "After": {"Time", "After"},
		"Now": {"Time", "Now"}, "Unix": {"Time", "Unix"}, "Date": {"Time", "Date"}, "Since": {"Time", "Since"},
		"FixedZone": {"Time", "FixedZone"}, "NewTicker": {"Time", "NewTicker"}, "NewTimer": {"Time", "NewTimer"},
		"Parse": {"Time", "Parse"}, "LoadLocation": {"Time", "LoadLocation"}, "ParseDuration": {"Time", "ParseDuration"}, "ParseInLocation": {"Time", "ParseInLocation"},
		"Tick": {"Time", "Tick"}, "AfterFunc": {"Time", "AfterFunc"},
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
		"ReadFile": {"Os", "ReadFile"}, "WriteFile": {"Os", "WriteFile"}, "Open": {"Os", "Open"},
		"Create": {"Os", "Create"}, "OpenFile": {"Os", "OpenFile"}, "Remove": {"Os", "Remove"}, "RemoveAll": {"Os", "RemoveAll"}, "NewFile": {"Os", "NewFile"}, "CreateTemp": {"Os", "CreateTemp"}, "TempDir": {"Os", "TempDir"},
		"Stat": {"Os", "Stat"}, "IsNotExist": {"Os", "IsNotExist"}, "MkdirAll": {"Os", "MkdirAll"},
	},
	"bytes": {
		"Equal": {"Bytes", "Equal"}, "Compare": {"Bytes", "Compare"}, "Contains": {"Bytes", "Contains"},
		"HasPrefix": {"Bytes", "HasPrefix"}, "HasSuffix": {"Bytes", "HasSuffix"}, "Index": {"Bytes", "Index"},
		"LastIndex": {"Bytes", "LastIndex"}, "LastIndexByte": {"Bytes", "LastIndexByte"}, "Replace": {"Bytes", "Replace"}, "ReplaceAll": {"Bytes", "ReplaceAll"}, "Clone": {"Bytes", "Clone"},
		"IndexByte": {"Bytes", "IndexByte"}, "IndexRune": {"Bytes", "IndexRune"}, "IndexAny": {"Bytes", "IndexAny"}, "Runes": {"Bytes", "Runes"}, "Count": {"Bytes", "Count"}, "ToUpper": {"Bytes", "ToUpper"},
		"ToLower": {"Bytes", "ToLower"}, "TrimSpace": {"Bytes", "TrimSpace"}, "Trim": {"Bytes", "Trim"}, "TrimPrefix": {"Bytes", "TrimPrefix"}, "TrimSuffix": {"Bytes", "TrimSuffix"}, "Repeat": {"Bytes", "Repeat"},
		"Split": {"Bytes", "Split"}, "SplitAfterN": {"Bytes", "SplitAfterN"}, "Join": {"Bytes", "Join"},
		"NewReader": {"Readers", "NewBytesReader"}, "NewBuffer": {"BytesBuffer", "NewBuffer"}, "NewBufferString": {"BytesBuffer", "NewBufferString"},
	},
	"strconv": {
		"Itoa": {"Strconv", "Itoa"}, "Atoi": {"Strconv", "Atoi"},
		"FormatInt": {"Strconv", "FormatInt"}, "FormatUint": {"Strconv", "FormatUint"},
		"FormatBool": {"Strconv", "FormatBool"}, "FormatFloat": {"Strconv", "FormatFloat"},
		"ParseInt": {"Strconv", "ParseInt"}, "ParseUint": {"Strconv", "ParseUint"},
		"ParseFloat": {"Strconv", "ParseFloat"}, "ParseBool": {"Strconv", "ParseBool"},
		"Quote": {"Strconv", "Quote"}, "QuoteToASCII": {"Strconv", "QuoteToASCII"},
		"CanBackquote": {"Strconv", "CanBackquote"}, "AppendInt": {"Strconv", "AppendInt"}, "AppendUint": {"Strconv", "AppendUint"}, "AppendBool": {"Strconv", "AppendBool"}, "AppendFloat": {"Strconv", "AppendFloat"}, "AppendQuote": {"Strconv", "AppendQuote"},
	},
	"unicode/utf8": {
		"RuneCountInString": {"Utf8", "RuneCountInString"}, "RuneCount": {"Utf8", "RuneCount"},
		"ValidString": {"Utf8", "ValidString"}, "ValidRune": {"Utf8", "ValidRune"}, "RuneLen": {"Utf8", "RuneLen"},
		"Valid": {"Utf8", "Valid"}, "EncodeRune": {"Utf8", "EncodeRune"},
		"DecodeRuneInString": {"Utf8", "DecodeRuneInString"}, "DecodeRune": {"Utf8", "DecodeRune"},
		"DecodeLastRuneInString": {"Utf8", "DecodeLastRuneInString"}, "DecodeLastRune": {"Utf8", "DecodeLastRune"}, "FullRune": {"Utf8", "FullRune"}, "FullRuneInString": {"Utf8", "FullRuneInString"}, "RuneStart": {"Utf8", "RuneStart"},
	},
	"unicode/utf16": {
		"EncodeRune": {"Utf16", "EncodeRune"}, "DecodeRune": {"Utf16", "DecodeRune"},
		"IsSurrogate": {"Utf16", "IsSurrogate"}, "Encode": {"Utf16", "Encode"}, "Decode": {"Utf16", "Decode"},
	},
	"strings": {
		"ToUpper": {"Strings", "ToUpper"}, "ToLower": {"Strings", "ToLower"}, "Title": {"Strings", "Title"},
		"Contains": {"Strings", "Contains"}, "HasPrefix": {"Strings", "HasPrefix"}, "HasSuffix": {"Strings", "HasSuffix"},
		"EqualFold": {"Strings", "EqualFold"}, "Index": {"Strings", "Index"}, "LastIndex": {"Strings", "LastIndex"}, "Compare": {"Strings", "Compare"},
		"IndexByte": {"Strings", "IndexByte"}, "Count": {"Strings", "Count"}, "Repeat": {"Strings", "Repeat"},
		"Replace": {"Strings", "Replace"}, "ReplaceAll": {"Strings", "ReplaceAll"}, "TrimSpace": {"Strings", "TrimSpace"},
		"Trim": {"Strings", "Trim"}, "TrimLeft": {"Strings", "TrimLeft"}, "TrimRight": {"Strings", "TrimRight"},
		"TrimPrefix": {"Strings", "TrimPrefix"}, "TrimSuffix": {"Strings", "TrimSuffix"},
		"Split": {"Strings", "Split"}, "SplitN": {"Strings", "SplitN"}, "Fields": {"Strings", "Fields"},
		"Join": {"Strings", "Join"}, "NewReader": {"Readers", "NewStringReader"}, "Cut": {"Strings", "Cut"}, "IndexRune": {"Strings", "IndexRune"},
		"ContainsRune": {"Strings", "ContainsRune"}, "ContainsAny": {"Strings", "ContainsAny"},
		"IndexAny": {"Strings", "IndexAny"}, "LastIndexByte": {"Strings", "LastIndexByte"},
		"ToTitle": {"Strings", "ToTitle"}, "SplitAfter": {"Strings", "SplitAfter"}, "SplitAfterN": {"Strings", "SplitAfterN"}, "Map": {"Strings", "Map"},
		"TrimFunc": {"Strings", "TrimFunc"}, "TrimLeftFunc": {"Strings", "TrimLeftFunc"}, "TrimRightFunc": {"Strings", "TrimRightFunc"},
		"IndexFunc": {"Strings", "IndexFunc"}, "FieldsFunc": {"Strings", "FieldsFunc"},
		"NewReplacer": {"Strings", "NewReplacer"},
	},
}

// opaqueShimTypes are stdlib types represented at runtime as opaque object
// handles (not lowered structures); method calls on them dispatch to shims.
var opaqueShimTypes = map[string]bool{
	"reflect.Type":                 true,
	"reflect.Value":                true,
	"sync.Mutex":                   true,
	"sync.Cond":                    true,
	"sync/atomic.Value":            true,
	"sync/atomic.Bool":             true,
	"sync/atomic.Int64":            true,
	"sync/atomic.Int32":            true,
	"sync/atomic.Uint64":           true,
	"sync/atomic.Uint32":           true,
	"sync/atomic.Uintptr":          true,
	"sync/atomic.Pointer":          true,
	"sync.RWMutex":                 true,
	"sync.WaitGroup":               true,
	"sync.Once":                    true,
	"sync.Map":                     true,
	"sync.Pool":                    true,
	"strconv.NumError":             true,
	"strings.Builder":              true,
	"strings.Replacer":             true,
	"bytes.Buffer":                 true,
	"os.File":                      true,
	"os.FileInfo":                  true,
	"crypto/sha3.SHAKE":            true,
	"io/fs.FileInfo":               true,
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
	"net.IPNet":                    true,
	"net.OpError":                  true,
	"encoding/xml.Encoder":         true,
	"encoding/xml.Decoder":         true,
	"encoding/xml.Name":            true,
	"encoding/xml.StartElement":    true,
	"encoding/xml.EndElement":      true,
	"encoding/xml.Attr":            true,
	"os.SyscallError":              true,
	"syscall.Flock_t":              true,
	"net/mail.Address":             true,
	"html/template.Template":       true,
	"text/template.Template":       true,
	"net.TCPAddr":                  true,
	"net.UDPAddr":                  true,
	"net.IPAddr":                   true,
	"net.UnixAddr":                 true,
	"net.PacketConn":               true,
	"net/http.ResponseWriter":      true,
	"net/http.Request":             true,
	"mime/multipart.Form":          true,
	"net/http.Server":              true,
	"log.Logger":                   true,
	"net/http.Transport":           true,
	"net/http.ServeMux":            true,
	"net/http.HTTP2Config":         true,
	"net/http.Protocols":           true,
	"crypto/tls.Config":            true,
	"crypto/tls.Conn":              true,
	"crypto/tls.ConnectionState":   true,
	"crypto/tls.Dialer":            true,
	"net/http.ResponseController":  true,
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
	"hash.Hash32":                  true,
	"hash.Hash64":                  true,
	"math/big.Int":                 true,
	"math/big.Float":               true,
	"hash/maphash.Hash":            true,
	"encoding/base32.Encoding":     true,
	"strings.Reader":               true,
	"bytes.Reader":                 true,
	"bufio.Scanner":                true,
	"bufio.Reader":                 true,
	"bufio.ReadWriter":             true,
	"mime/multipart.FileHeader":    true,
	"mime/multipart.File":          true,
	"net/http.Cookie":              true,
	"bufio.Writer":                 true,
	"time.Ticker":                  true,
	"time.Timer":                   true,
	"encoding/json.Decoder":        true,
	"encoding/json.Encoder":        true,
	"reflect.StructField":          true,
	"reflect.Method":               true,
	"runtime.Func":                 true,
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
	"io.EOF":                         {"Io", "EOF"},
	"io.ErrUnexpectedEOF":            {"Io", "ErrUnexpectedEOF"},
	"net.ErrClosed":                  {"Net", "ErrClosed"},
	"net/http.ErrAbortHandler":       {"Http", "ErrAbortHandler"},
	"net/http.ErrBodyNotAllowed":     {"Http", "ErrBodyNotAllowed"},
	"net/http.ErrNotSupported":       {"Http", "ErrNotSupported"},
	"net/http.ErrSkipAltProtocol":    {"Http", "ErrSkipAltProtocol"},
	"net/http.ErrServerClosed":       {"Http", "ErrServerClosed"},
	"net/http.ErrHandlerTimeout":     {"Http", "ErrHandlerTimeout"},
	"net/http.NoBody":                {"Http", "NoBody"},
	"net/http.DefaultServeMux":       {"Http", "DefaultServeMux"},
	"net/http.LocalAddrContextKey":   {"Http", "LocalAddrContextKey"},
	"net/http.ServerContextKey":      {"Http", "ServerContextKey"},
	"os.ErrDeadlineExceeded":         {"Os", "ErrDeadlineExceeded"},
	"os.ErrNotExist":                 {"Os", "ErrNotExist"},
	"os.ErrExist":                    {"Os", "ErrExist"},
	"os.ErrClosed":                   {"Os", "ErrClosed"},
	"encoding/xml.Header":            {"Xml", "Header"},
	"io/fs.ErrClosed":                {"Os", "ErrClosed"},
	"io/fs.ErrNotExist":              {"Os", "ErrNotExist"},
	"io/fs.ErrExist":                 {"Os", "ErrExist"},
	"io.ErrShortWrite":               {"Io", "ErrShortWrite"},
	"io.ErrShortBuffer":              {"Io", "ErrShortBuffer"},
	"io.ErrClosedPipe":               {"Io", "ErrClosedPipe"},
	"io.ErrNoProgress":               {"Io", "ErrNoProgress"},
	"net/http.ErrNotMultipart":       {"Http", "ErrNotMultipart"},
	"strconv.ErrRange":               {"Strconv", "ErrRangeVar"},
	"strconv.ErrSyntax":              {"Strconv", "ErrSyntaxVar"},
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
	"bufio.ReadWriter": {
		"Reader": {"Bufio", "RW_Reader"}, "Writer": {"Bufio", "RW_Writer"},
	},
	"mime/multipart.FileHeader": {
		"Filename": {"Multipart", "FH_Filename"}, "Size": {"Multipart", "FH_Size"}, "Header": {"Multipart", "FH_Header"},
	},
	"net/http.Cookie": {
		"Name": {"Http", "Cookie_Name"}, "Value": {"Http", "Cookie_Value"}, "Path": {"Http", "Cookie_Path"},
		"Domain": {"Http", "Cookie_Domain"}, "MaxAge": {"Http", "Cookie_MaxAge"}, "Secure": {"Http", "Cookie_Secure"}, "HttpOnly": {"Http", "Cookie_HttpOnly"},
	},
	"sync.Pool": {
		"New": {"Sync", "Pool_New"},
	},
	"net.OpError": {
		"Op": {"Net", "OpError_Op"}, "Net": {"Net", "OpError_Net"}, "Err": {"Net", "OpError_Err"},
	},
	"os.SyscallError": {
		"Syscall": {"Os", "SyscallError_Syscall"}, "Err": {"Os", "SyscallError_Err"},
	},
	"encoding/xml.Name": {
		"Space": {"Xml", "Name_Space"}, "Local": {"Xml", "Name_Local"},
	},
	"encoding/xml.StartElement": {
		"Name": {"Xml", "Start_Name"}, "Attr": {"Xml", "Start_Attr"},
	},
	"encoding/xml.EndElement": {
		"Name": {"Xml", "End_Name"},
	},
	"encoding/xml.Attr": {
		"Name": {"Xml", "Attr_Name"}, "Value": {"Xml", "Attr_Value"},
	},
	"strconv.NumError": {
		"Err": {"Strconv", "NumError_Err"}, "Func": {"Strconv", "NumError_Func"}, "Num": {"Strconv", "NumError_Num"},
	},
	"net/url.URL": {
		"Scheme": {"Url", "URL_Scheme"}, "Host": {"Url", "URL_Host"}, "Path": {"Url", "URL_Path"},
		"RawQuery": {"Url", "URL_RawQuery"}, "Fragment": {"Url", "URL_Fragment"},
		"User": {"Url", "URL_User"}, "RawPath": {"Url", "URL_Path"}, "Opaque": {"Url", "URL_Opaque"},
	},
	"net.IPNet": {
		"IP": {"Net", "IPNet_IP"},
	},
	"sync.Cond": {
		"L": {"Sync", "Cond_L"},
	},
	"net/mail.Address": {
		"Name": {"Mail", "Address_Name"}, "Address": {"Mail", "Address_Address"},
	},
	"net/http.Server": {
		"TLSConfig": {"HttpTypes", "Server_TLSConfig"}, "TLSNextProto": {"HttpTypes", "Server_TLSNextProto"}, "Handler": {"HttpTypes", "Server_Handler"},
		"ErrorLog": {"HttpTypes", "Server_ErrorLog"}, "BaseContext": {"HttpTypes", "Server_BaseContext"}, "ConnState": {"HttpTypes", "Server_ConnState"},
		"ReadTimeout": {"HttpTypes", "Server_ReadTimeout"}, "ReadHeaderTimeout": {"HttpTypes", "Server_ReadHeaderTimeout"}, "WriteTimeout": {"HttpTypes", "Server_WriteTimeout"},
		"IdleTimeout": {"HttpTypes", "Server_IdleTimeout"}, "MaxHeaderBytes": {"HttpTypes", "Server_MaxHeaderBytes"}, "HTTP2": {"HttpTypes", "Server_HTTP2"},
		"Addr": {"HttpTypes", "Server_Addr"},
	},
	"net/http.HTTP2Config": {
		"MaxConcurrentStreams": {"HttpTypes", "H2C_MaxConcurrentStreams"}, "MaxDecoderHeaderTableSize": {"HttpTypes", "H2C_MaxDecoderHeaderTableSize"}, "MaxEncoderHeaderTableSize": {"HttpTypes", "H2C_MaxEncoderHeaderTableSize"},
		"MaxReadFrameSize": {"HttpTypes", "H2C_MaxReadFrameSize"}, "MaxReceiveBufferPerConnection": {"HttpTypes", "H2C_MaxReceiveBufferPerConnection"}, "MaxReceiveBufferPerStream": {"HttpTypes", "H2C_MaxReceiveBufferPerStream"},
		"MaxUploadBufferPerConnection": {"HttpTypes", "H2C_MaxUploadBufferPerConnection"}, "MaxUploadBufferPerStream": {"HttpTypes", "H2C_MaxUploadBufferPerStream"},
		"PermitProhibitedCipherSuites": {"HttpTypes", "H2C_PermitProhibitedCipherSuites"}, "StrictMaxConcurrentStreams": {"HttpTypes", "H2C_StrictMaxConcurrentStreams"}, "StrictMaxConcurrentRequests": {"HttpTypes", "H2C_StrictMaxConcurrentRequests"},
		"PingTimeout": {"HttpTypes", "H2C_PingTimeout"}, "ReadIdleTimeout": {"HttpTypes", "H2C_ReadIdleTimeout"}, "SendPingTimeout": {"HttpTypes", "H2C_SendPingTimeout"}, "WriteByteTimeout": {"HttpTypes", "H2C_WriteByteTimeout"}, "CountError": {"HttpTypes", "H2C_CountError"},
	},
	"crypto/tls.ConnectionState": {
		"NegotiatedProtocol": {"HttpTypes", "CS_NegotiatedProtocol"}, "ServerName": {"HttpTypes", "CS_ServerName"}, "Version": {"HttpTypes", "CS_Version"},
		"CipherSuite": {"HttpTypes", "CS_CipherSuite"}, "HandshakeComplete": {"HttpTypes", "CS_HandshakeComplete"}, "DidResume": {"HttpTypes", "CS_DidResume"}, "PeerCertificates": {"HttpTypes", "CS_PeerCertificates"},
		"NegotiatedProtocolIsMutual": {"HttpTypes", "CS_NegotiatedProtocolIsMutual"},
	},
	"crypto/tls.Config": {
		"NextProtos": {"HttpTypes", "Config_NextProtos"}, "CipherSuites": {"HttpTypes", "Config_CipherSuites"},
		"MinVersion": {"HttpTypes", "Config_MinVersion"}, "MaxVersion": {"HttpTypes", "Config_MaxVersion"},
		"InsecureSkipVerify": {"HttpTypes", "Config_InsecureSkipVerify"}, "GetCertificate": {"HttpTypes", "Config_GetCertificate"}, "PreferServerCipherSuites": {"HttpTypes", "Config_PreferServerCipherSuites"},
		"ServerName": {"HttpTypes", "Config_ServerName"}, "RootCAs": {"HttpTypes", "Config_RootCAs"}, "Certificates": {"HttpTypes", "Config_Certificates"}, "ClientAuth": {"HttpTypes", "Config_ClientAuth"},
	},
	"net/http.Transport": {
		"HTTP2": {"HttpTypes", "Transport_HTTP2"}, "TLSClientConfig": {"HttpTypes", "Transport_TLSClientConfig"}, "Proxy": {"HttpTypes", "Transport_Proxy"},
		"DialContext": {"HttpTypes", "Transport_DialContext"}, "DialTLSContext": {"HttpTypes", "Transport_DialTLSContext"},
		"MaxHeaderListSize": {"HttpTypes", "Transport_MaxHeaderListSize"}, "ExpectContinueTimeout": {"HttpTypes", "Transport_ExpectContinueTimeout"},
		"DisableCompression": {"HttpTypes", "Transport_DisableCompression"}, "DisableKeepAlives": {"HttpTypes", "Transport_DisableKeepAlives"},
		"ForceAttemptHTTP2": {"HttpTypes", "Transport_ForceAttemptHTTP2"}, "TLSNextProto": {"HttpTypes", "Transport_TLSNextProto"},
		"MaxResponseHeaderBytes": {"HttpTypes", "Transport_MaxResponseHeaderBytes"}, "IdleConnTimeout": {"HttpTypes", "Transport_IdleConnTimeout"}, "ResponseHeaderTimeout": {"HttpTypes", "Transport_ResponseHeaderTimeout"},
	},
	"net/http.Response": {
		"StatusCode": {"Http", "Resp_StatusCode"}, "Status": {"Http", "Resp_Status"},
		"Body": {"Http", "Resp_Body"}, "ContentLength": {"Http", "Resp_ContentLength"},
		"Request": {"Http", "Resp_Request"}, "TLS": {"Http", "Resp_TLS"}, "Trailer": {"Http", "Resp_Trailer"}, "Uncompressed": {"Http", "Resp_Uncompressed"}, "Header": {"Http", "Resp_Header"},
		"Proto": {"Http", "Resp_Proto"}, "ProtoMajor": {"Http", "Resp_ProtoMajor"}, "ProtoMinor": {"Http", "Resp_ProtoMinor"},
	},
	"container/list.Element": {
		"Value": {"List", "Element_Value"},
	},
	"time.Ticker": {
		"C": {"Time", "Ticker_C"},
	},
	"time.Timer": {
		"C": {"Time", "Ticker_C"},
	},
	"reflect.StructField": {
		"Name": {"Reflect", "StructField_Name"}, "Tag": {"Reflect", "StructField_Tag"},
		"Anonymous": {"Reflect", "StructField_Anonymous"}, "Index": {"Reflect", "StructField_Index"},
		"Type": {"Reflect", "StructField_Type"}, "PkgPath": {"Reflect", "StructField_PkgPath"},
	},
	"reflect.Method": {
		"Name": {"Reflect", "Method_Name"}, "Index": {"Reflect", "Method_Index"},
		"PkgPath": {"Reflect", "Method_PkgPath"},
	},
	"mime/multipart.Form": {
		"Value": {"Multipart", "Form_Value"}, "File": {"Multipart", "Form_File"},
	},
	"net/http.Request": {
		"Method": {"Http", "Req_Method"}, "URL": {"Http", "Req_URL"}, "Body": {"Http", "Req_Body"},
		"Host": {"Http", "Req_Host"}, "RemoteAddr": {"Http", "Req_RemoteAddr"}, "Form": {"Http", "Req_Form"}, "PostForm": {"Http", "Req_PostForm"}, "Header": {"Http", "Req_Header"},
		"ContentLength": {"Http", "Req_ContentLength"}, "Trailer": {"Http", "Req_Trailer"}, "TLS": {"Http", "Req_TLS"}, "MultipartForm": {"Http", "Req_MultipartForm"},
		"Proto": {"Http", "Req_Proto"}, "ProtoMajor": {"Http", "Req_ProtoMajor"}, "ProtoMinor": {"Http", "Req_ProtoMinor"}, "RequestURI": {"Http", "Req_RequestURI"}, "Context": {"Http", "Req_Context"},
		"Cancel": {"Http", "Req_Cancel"}, "GetBody": {"Http", "Req_GetBody"}, "Close": {"Http", "Req_Close"},
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

// shimFieldSetRegistry maps "importpath.Type" to its writable fields, each lowering
// to a setter (object receiver, value) -> void extern.
// opaqueShimClone maps an opaque mutable shim to its value-copy cloner, used when
// *p produces a value copy (u := *p) that must not alias the original.
var opaqueShimClone = map[string]shimFunc{
	"net/url.URL": {"Url", "URL_Clone"},
}

var shimFieldSetRegistry = map[string]map[string]shimFunc{
	"sync.Cond": {"L": {"Sync", "Cond_SetL"}},
	"sync.Pool": {"New": {"Sync", "Pool_SetNew"}},
	"encoding/xml.Name": {
		"Space": {"Xml", "Name_SetSpace"}, "Local": {"Xml", "Name_SetLocal"},
	},
	"encoding/xml.StartElement": {
		"Name": {"Xml", "Start_SetName"}, "Attr": {"Xml", "Start_SetAttr"},
	},
	"encoding/xml.EndElement": {
		"Name": {"Xml", "End_SetName"},
	},
	"encoding/xml.Attr": {
		"Name": {"Xml", "Attr_SetName"}, "Value": {"Xml", "Attr_SetValue"},
	},
	"net/http.Cookie": {
		"Name": {"Http", "Cookie_SetName"}, "Value": {"Http", "Cookie_SetValue"}, "Path": {"Http", "Cookie_SetPath"},
		"Domain": {"Http", "Cookie_SetDomain"}, "MaxAge": {"Http", "Cookie_SetMaxAge"}, "Secure": {"Http", "Cookie_SetSecure"}, "HttpOnly": {"Http", "Cookie_SetHttpOnly"},
	},
	"net/http.Request": {
		"ContentLength": {"Http", "Req_SetContentLength"}, "Trailer": {"Http", "Req_SetTrailer"}, "TLS": {"Http", "Req_SetTLS"}, "Body": {"Http", "Req_SetBody"},
	},
	"net/http.Response": {
		"StatusCode": {"Http", "Resp_SetStatusCode"}, "Status": {"Http", "Resp_SetStatus"}, "ContentLength": {"Http", "Resp_SetContentLength"}, "Body": {"Http", "Resp_SetBody"},
		"Request": {"Http", "Resp_SetRequest"}, "TLS": {"Http", "Resp_SetTLS"}, "Trailer": {"Http", "Resp_SetTrailer"}, "Uncompressed": {"Http", "Resp_SetUncompressed"}, "Header": {"Http", "Resp_SetHeader"},
	},
	"net/http.Server": {
		"TLSConfig": {"HttpTypes", "Server_SetTLSConfig"}, "TLSNextProto": {"HttpTypes", "Server_SetTLSNextProto"}, "IdleTimeout": {"HttpTypes", "Server_SetIdleTimeout"},
		"Addr": {"HttpTypes", "Server_SetAddr"}, "Handler": {"HttpTypes", "Server_SetHandler"},
	},
	"net/http.Transport": {
		"TLSNextProto": {"HttpTypes", "Transport_SetTLSNextProto"}, "TLSClientConfig": {"HttpTypes", "Transport_SetTLSClientConfig"}, "HTTP2": {"HttpTypes", "Transport_SetHTTP2"},
	},
	"crypto/tls.Config": {
		"NextProtos": {"HttpTypes", "Config_SetNextProtos"}, "PreferServerCipherSuites": {"HttpTypes", "Config_SetPreferServerCipherSuites"},
		"ServerName": {"HttpTypes", "Config_SetServerName"}, "MinVersion": {"HttpTypes", "Config_SetMinVersion"}, "MaxVersion": {"HttpTypes", "Config_SetMaxVersion"}, "InsecureSkipVerify": {"HttpTypes", "Config_SetInsecureSkipVerify"},
	},
	"net/url.URL": {
		"Path": {"Url", "URL_SetPath"}, "Scheme": {"Url", "URL_SetScheme"},
		"Host": {"Url", "URL_SetHost"}, "RawQuery": {"Url", "URL_SetRawQuery"},
		"Fragment": {"Url", "URL_SetFragment"},
	},
}

// shimFieldSetExtern returns the setter extern for a writable opaque-shim field.
func shimFieldSetExtern(shim, field string, valT goir.Type) (*goir.Extern, bool) {
	fm, ok := shimFieldSetRegistry[shim]
	if !ok {
		return nil, false
	}
	sf, ok := fm[field]
	if !ok {
		return nil, false
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Params: []goir.Type{goir.TObject, valT}, Ret: goir.TVoid}, true
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
	"sync.Mutex":                 {"Sync", "NewMutex"},
	"sync.RWMutex":               {"Sync", "NewRWMutex"},
	"sync.WaitGroup":             {"Sync", "NewWaitGroup"},
	"sync.Once":                  {"Sync", "NewOnce"},
	"sync.Map":                   {"Sync", "NewMap"},
	"sync.Pool":                  {"Sync", "NewPool"},
	"sync.Cond":                  {"Sync", "NewCondZero"},
	"sync/atomic.Value":          {"Atomic", "NewValue"},
	"net/http.Server":            {"HttpTypes", "NewServer"},
	"log.Logger":                 {"Log", "NewLoggerZero"},
	"net/http.Transport":         {"HttpTypes", "NewTransport"},
	"crypto/tls.Config":          {"HttpTypes", "NewTlsConfig"},
	"crypto/tls.Conn":            {"HttpTypes", "NewTlsConn"},
	"crypto/tls.ConnectionState": {"HttpTypes", "NewTlsConnState"},
	"net/http.HTTP2Config":       {"HttpTypes", "NewHTTP2Config"},
	"net/http.Protocols":         {"HttpTypes", "NewProtocols"},
	"sync/atomic.Bool":           {"Atomic", "NewBool"},
	"sync/atomic.Int64":          {"AtomicInt", "NewInt"},
	"sync/atomic.Int32":          {"AtomicInt", "NewInt"},
	"sync/atomic.Uint64":         {"AtomicInt", "NewUint"},
	"sync/atomic.Uint32":         {"AtomicInt", "NewUint"},
	"sync/atomic.Uintptr":        {"AtomicInt", "NewUint"},
	"sync/atomic.Pointer":        {"AtomicInt", "NewPointer"},
	"strings.Builder":            {"StringsBuilder", "New"},
	"bytes.Buffer":               {"BytesBuffer", "New"},
	"time.Time":                  {"Time", "TimeZero"},
	"math/big.Int":               {"Big", "IntZero"},
	"math/big.Float":             {"Big", "FloatZero"},
	"hash/maphash.Hash":          {"MapHash", "New"},
	"net.IPNet":                  {"Net", "NewIPNet"},
	"syscall.Flock_t":            {"Syscall", "NewFlockT"},
	"encoding/xml.Name":          {"Xml", "NewXmlName"},
	"encoding/xml.StartElement":  {"Xml", "NewXmlStart"},
	"encoding/xml.EndElement":    {"Xml", "NewXmlEnd"},
	"encoding/xml.Attr":          {"Xml", "NewXmlAttr"},
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
	"net/http.ServeMux": {
		"Handle": {"Http", "Mux_Handle"}, "HandleFunc": {"Http", "Mux_HandleFunc"}, "ServeHTTP": {"Http", "Mux_ServeHTTP"}, "Handler": {"Http", "Mux_Handler"},
	},
	"mime/multipart.Form": {
		"RemoveAll": {"Multipart", "Form_RemoveAll"},
	},
	"crypto/tls.Config": {
		"Clone": {"HttpTypes", "Config_Clone"},
	},
	"crypto/tls.Dialer": {
		"DialContext": {"HttpTypes", "Dialer_DialContext"}, "Dial": {"HttpTypes", "Dialer_Dial"},
	},
	"crypto/tls.Conn": {
		"Close": {"HttpTypes", "Conn_Close"}, "LocalAddr": {"HttpTypes", "Conn_LocalAddr"}, "RemoteAddr": {"HttpTypes", "Conn_RemoteAddr"},
		"Read": {"HttpTypes", "Conn_Read"}, "Write": {"HttpTypes", "Conn_Write"}, "Handshake": {"HttpTypes", "Conn_Handshake"}, "HandshakeContext": {"HttpTypes", "Conn_HandshakeContext"},
		"ConnectionState": {"HttpTypes", "Conn_ConnectionState"}, "SetDeadline": {"HttpTypes", "Conn_SetDeadline"}, "SetReadDeadline": {"HttpTypes", "Conn_SetReadDeadline"}, "SetWriteDeadline": {"HttpTypes", "Conn_SetWriteDeadline"}, "NetConn": {"HttpTypes", "Conn_NetConn"},
	},
	"log.Logger": {
		"Print": {"Log", "Logger_Print"}, "Println": {"Log", "Logger_Println"}, "Printf": {"Log", "Logger_Printf"}, "Output": {"Log", "Logger_Output"},
		"Fatal": {"Log", "Logger_Fatal"}, "Fatalf": {"Log", "Logger_Fatalf"}, "Fatalln": {"Log", "Logger_Fatalln"}, "Panic": {"Log", "Logger_Panic"}, "Panicf": {"Log", "Logger_Panicf"},
		"SetFlags": {"Log", "Logger_SetFlags"}, "Flags": {"Log", "Logger_Flags"}, "SetPrefix": {"Log", "Logger_SetPrefix"}, "Prefix": {"Log", "Logger_Prefix"}, "SetOutput": {"Log", "Logger_SetOutput"}, "Writer": {"Log", "Logger_Writer"},
	},
	"net/http.Server": {
		"RegisterOnShutdown": {"HttpTypes", "Server_RegisterOnShutdown"}, "Serve": {"HttpTypes", "Server_Serve"}, "SetKeepAlivesEnabled": {"HttpTypes", "Server_SetKeepAlivesEnabled"},
		"ListenAndServe": {"HttpTypes", "Server_ListenAndServe"}, "ListenAndServeTLS": {"HttpTypes", "Server_ListenAndServeTLS"}, "Shutdown": {"HttpTypes", "Server_Shutdown"}, "Close": {"HttpTypes", "Server_Close"},
	},
	"net/http.Transport": {
		"RegisterProtocol": {"HttpTypes", "Transport_RegisterProtocol"}, "CloseIdleConnections": {"HttpTypes", "Transport_CloseIdleConnections"}, "Clone": {"HttpTypes", "Transport_Clone"},
	},
	"net/http.ResponseController": {
		"Hijack": {"Http", "RC_Hijack"}, "Flush": {"Http", "RC_Flush"}, "SetReadDeadline": {"Http", "RC_SetReadDeadline"}, "SetWriteDeadline": {"Http", "RC_SetWriteDeadline"}, "EnableFullDuplex": {"Http", "RC_EnableFullDuplex"},
	},
	"net/http.Protocols": {
		"SetHTTP1": {"HttpTypes", "Proto_SetHTTP1"}, "SetHTTP2": {"HttpTypes", "Proto_SetHTTP2"}, "SetUnencryptedHTTP2": {"HttpTypes", "Proto_SetUnencryptedHTTP2"},
		"HTTP1": {"HttpTypes", "Proto_HTTP1"}, "HTTP2": {"HttpTypes", "Proto_HTTP2"}, "UnencryptedHTTP2": {"HttpTypes", "Proto_UnencryptedHTTP2"},
	},
	"net/http.Header": {
		"Get": {"Http", "Header_Get"}, "Set": {"Http", "Header_Set"}, "Add": {"Http", "Header_Add"}, "Del": {"Http", "Header_Del"}, "Values": {"Http", "Header_Values"}, "Clone": {"Http", "Header_Clone"}, "Write": {"Http", "Header_Write"},
	},
	"html/template.Template": {
		"New": {"Template", "Tmpl_New"}, "Delims": {"Template", "Tmpl_Delims"}, "Funcs": {"Template", "Tmpl_Funcs"},
		"Parse": {"Template", "Tmpl_Parse"}, "ParseFiles": {"Template", "Tmpl_ParseFiles"}, "ParseGlob": {"Template", "Tmpl_ParseGlob"},
		"Execute": {"Template", "Tmpl_Execute"}, "ExecuteTemplate": {"Template", "Tmpl_ExecuteTemplate"},
		"Templates": {"Template", "Tmpl_Templates"}, "Name": {"Template", "Tmpl_Name"}, "Lookup": {"Template", "Tmpl_Lookup"}, "Option": {"Template", "Tmpl_Option"},
	},
	"text/template.Template": {
		"New": {"Template", "Tmpl_New"}, "Delims": {"Template", "Tmpl_Delims"}, "Funcs": {"Template", "Tmpl_Funcs"},
		"Parse": {"Template", "Tmpl_Parse"}, "ParseFiles": {"Template", "Tmpl_ParseFiles"}, "ParseGlob": {"Template", "Tmpl_ParseGlob"},
		"Execute": {"Template", "Tmpl_Execute"}, "ExecuteTemplate": {"Template", "Tmpl_ExecuteTemplate"},
		"Templates": {"Template", "Tmpl_Templates"}, "Name": {"Template", "Tmpl_Name"}, "Lookup": {"Template", "Tmpl_Lookup"}, "Option": {"Template", "Tmpl_Option"},
	},
	"encoding/json.Decoder": {
		"Token": {"Json", "Decoder_Token"}, "More": {"Json", "Decoder_More"},
		"Decode": {"Json", "Decoder_Decode"}, "UseNumber": {"Json", "Decoder_UseNumber"}, "DisallowUnknownFields": {"Json", "Decoder_DisallowUnknownFields"},
		"Buffered": {"Json", "Decoder_Buffered"},
	},
	"encoding/json.Encoder": {
		"Encode": {"Json", "Encoder_Encode"}, "SetIndent": {"Json", "Encoder_SetIndent"},
		"SetEscapeHTML": {"Json", "Encoder_SetEscapeHTML"},
	},
	"net/url.URL": {
		"IsAbs": {"Url", "URL_IsAbs"}, "String": {"Url", "URL_String"},
		"ResolveReference": {"Url", "URL_ResolveReference"}, "Query": {"Url", "URL_Query"}, "RequestURI": {"Url", "URL_RequestURI"},
	},
	"strings.Reader": {
		"ReadByte": {"Readers", "Reader_ReadByte"}, "UnreadByte": {"Readers", "Reader_UnreadByte"},
		"ReadRune": {"Readers", "Reader_ReadRune"}, "Len": {"Readers", "Reader_Len"}, "Size": {"Readers", "Reader_Size"},
	},
	"bytes.Reader": {
		"ReadByte": {"Readers", "Reader_ReadByte"}, "UnreadByte": {"Readers", "Reader_UnreadByte"},
		"ReadRune": {"Readers", "Reader_ReadRune"}, "Len": {"Readers", "Reader_Len"}, "Size": {"Readers", "Reader_Size"},
	},
	"reflect.Type": {
		"Kind": {"Reflect", "Type_Kind"}, "Name": {"Reflect", "Type_Name"},
		"String": {"Reflect", "Type_String"}, "NumField": {"Reflect", "Type_NumField"},
		"Elem": {"Reflect", "Type_Elem"}, "Key": {"Reflect", "Type_Key"}, "Len": {"Reflect", "Type_Len"},
		"Field": {"Reflect", "Type_Field"}, "NumMethod": {"Reflect", "Type_NumMethod"},
		"NumIn": {"Reflect", "Type_NumIn"}, "NumOut": {"Reflect", "Type_NumOut"},
		"In": {"Reflect", "Type_In"}, "Out": {"Reflect", "Type_Out"},
		"AssignableTo": {"Reflect", "Type_AssignableTo"}, "ConvertibleTo": {"Reflect", "Type_ConvertibleTo"},
		"Comparable": {"Reflect", "Type_Comparable"}, "Implements": {"Reflect", "Type_Implements"},
		"PkgPath": {"Reflect", "Type_PkgPath"}, "Method": {"Reflect", "Type_Method"},
	},
	"reflect.Kind": {
		"String": {"Reflect", "Kind_String"},
	},
	"reflect.StructTag": {
		"Get": {"Reflect", "StructTag_Get"}, "Lookup": {"Reflect", "StructTag_Lookup"},
	},
	"runtime.Func": {
		"Name": {"Goruntime", "Func_Name"}, "FileLine": {"Goruntime", "Func_FileLine"},
		"Entry": {"Goruntime", "Func_Entry"},
	},
	"bufio.Scanner": {
		"Scan": {"Bufio", "Scanner_Scan"}, "Text": {"Bufio", "Scanner_Text"}, "Bytes": {"Bufio", "Scanner_Bytes"}, "Err": {"Bufio", "Scanner_Err"},
	},
	"bufio.Reader": {
		"Read": {"Bufio", "Reader_Read"}, "ReadByte": {"Bufio", "Reader_ReadByte"}, "Reset": {"Bufio", "Reader_Reset"}, "Buffered": {"Bufio", "Reader_Buffered"},
	},
	"bufio.ReadWriter": {
		"Flush": {"Bufio", "RW_Flush"}, "Write": {"Bufio", "Writer_Write"}, "Read": {"Bufio", "RW_Read"},
	},
	"bufio.Writer": {
		"Available": {"Bufio", "Writer_Available"}, "Buffered": {"Bufio", "Writer_Buffered"}, "Flush": {"Bufio", "Writer_Flush"},
		"Write": {"Bufio", "Writer_Write"}, "WriteByte": {"Bufio", "Writer_WriteByte"}, "WriteString": {"Bufio", "Writer_WriteString"}, "Reset": {"Bufio", "Writer_Reset"},
	},
	"time.Ticker": {
		"Stop": {"Time", "Ticker_Stop"}, "Reset": {"Time", "Ticker_Reset"},
	},
	"time.Timer": {
		"Stop": {"Time", "Timer_Stop"}, "Reset": {"Time", "Timer_Reset"},
	},
	"mime/multipart.FileHeader": {
		"Open": {"Multipart", "FH_Open"},
	},
	"mime/multipart.File": {
		"Read": {"Multipart", "File_Read"}, "Close": {"Multipart", "File_Close"}, "Seek": {"Multipart", "File_Seek"},
	},
	"net/http.Cookie": {
		"String": {"Http", "Cookie_String"},
	},
	"encoding/xml.Encoder": {
		"Encode": {"Xml", "Encoder_Encode"}, "EncodeElement": {"Xml", "Encoder_EncodeElement"}, "EncodeToken": {"Xml", "Encoder_EncodeToken"},
		"Flush": {"Xml", "Encoder_Flush"}, "Indent": {"Xml", "Encoder_Indent"}, "Close": {"Xml", "Encoder_Close"},
	},
	"encoding/xml.Decoder": {
		"Decode": {"Xml", "Decoder_Decode"}, "Token": {"Xml", "Decoder_Token"},
	},
	"io.ReadCloser": {
		"Close": {"Http", "Body_Close"},
	},
	"net/http.Request": {
		"ParseForm": {"Http", "Req_ParseForm"}, "ParseMultipartForm": {"Http", "Req_ParseMultipartForm"}, "Context": {"Http", "Req_Context"},
		"WithContext": {"Http", "Req_WithContext"}, "Clone": {"Http", "Req_Clone"}, "UserAgent": {"Http", "Req_UserAgent"}, "Referer": {"Http", "Req_Referer"}, "Cookie": {"Http", "Req_Cookie"}, "Cookies": {"Http", "Req_Cookies"}, "FormFile": {"Http", "Req_FormFile"}, "MultipartReader": {"Http", "Req_MultipartReader"},
	},
	"net/http.ResponseWriter": {
		"Write": {"Http", "RW_Write"}, "WriteHeader": {"Http", "RW_WriteHeader"}, "Header": {"Http", "RW_Header"},
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
		"MoveToFront": {"List", "List_MoveToFront"}, "MoveToBack": {"List", "List_MoveToBack"},
		"Init": {"List", "List_Init"}, "InsertBefore": {"List", "List_InsertBefore"}, "InsertAfter": {"List", "List_InsertAfter"},
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
	"compress/gzip.Reader": {
		"Read": {"Compress", "CompR_Read"}, "Reset": {"Compress", "CompR_Reset"}, "Close": {"Compress", "CompR_Close"},
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
	"hash.Hash32": {
		"Write": {"Hashes", "H32_Write"}, "Sum32": {"Hashes", "H32_Sum32"}, "Size": {"Hashes", "H32_Size"}, "Reset": {"Hashes", "H32_Reset"},
	},
	"hash.Hash64": {
		"Write": {"Hashes", "H64_Write"}, "Sum64": {"Hashes", "H64_Sum64"}, "Size": {"Hashes", "H64_Size"}, "Reset": {"Hashes", "H64_Reset"},
	},
	"encoding/base64.Encoding": {
		"EncodeToString": {"Base64", "EncodeToString"}, "DecodeString": {"Base64", "DecodeString"},
		"EncodedLen": {"Base64", "EncodedLen"}, "DecodedLen": {"Base64", "DecodedLen"}, "Encode": {"Base64", "Encode"}, "Decode": {"Base64", "Decode"},
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
		"String": {"Regexp", "Re_String"}, "FindStringIndex": {"Regexp", "Re_FindStringIndex"}, "FindAllStringSubmatchIndex": {"Regexp", "Re_FindAllStringSubmatchIndex"},
		"SubexpNames": {"Regexp", "Re_SubexpNames"}, "NumSubexp": {"Regexp", "Re_NumSubexp"},
		"FindStringSubmatchIndex": {"Regexp", "Re_FindStringSubmatchIndex"},
		"FindReaderSubmatchIndex": {"Regexp", "Re_FindReaderSubmatchIndex"},
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
		"SetInt64": {"Big", "Int_SetInt64"}, "SetUint64": {"Big", "Int_SetUint64"},
		"Lsh": {"Big", "Int_Lsh"}, "Rsh": {"Big", "Int_Rsh"}, "SetBytes": {"Big", "Int_SetBytes"},
		"Bytes": {"Big", "Int_Bytes"}, "Text": {"Big", "Int_Text"}, "DivMod": {"Big", "Int_DivMod"},
		"Uint64": {"Big", "Int_Uint64"}, "And": {"Big", "Int_And"}, "Or": {"Big", "Int_Or"},
		"Xor": {"Big", "Int_Xor"}, "Not": {"Big", "Int_Not"}, "BitLen": {"Big", "Int_BitLen"},
		"IsInt64": {"Big", "Int_IsInt64"}, "IsUint64": {"Big", "Int_IsUint64"},
		"CmpAbs": {"Big", "Int_CmpAbs"}, "Sqrt": {"Big", "Int_Sqrt"}, "ProbablyPrime": {"Big", "Int_ProbablyPrime"},
		"QuoRem": {"Big", "Int_QuoRem"},
	},
	"math/big.Float": {
		"SetInt": {"Big", "Float_SetInt"}, "Sub": {"Big", "Float_Sub"}, "Cmp": {"Big", "Float_Cmp"},
		"Sign": {"Big", "Float_Sign"}, "IsInt": {"Big", "Float_IsInt"}, "String": {"Big", "Float_String"},
		"Text": {"Big", "Float_Text"}, "Int": {"Big", "Float_Int"}, "SetString": {"Big", "Float_SetString"},
	},
	"hash/maphash.Hash": {
		"WriteByte": {"MapHash", "WriteByte"}, "Write": {"MapHash", "Write"}, "WriteString": {"MapHash", "WriteString"},
		"Sum64": {"MapHash", "Sum64"}, "Reset": {"MapHash", "Reset"}, "Size": {"MapHash", "Size"}, "BlockSize": {"MapHash", "BlockSize"},
	},
	"strings.Replacer": {
		"Replace": {"Strings", "Replacer_Replace"}, "WriteString": {"Strings", "Replacer_WriteString"},
	},
	"strings.Builder": {
		"WriteString": {"StringsBuilder", "WriteString"}, "WriteByte": {"StringsBuilder", "WriteByte"},
		"WriteRune": {"StringsBuilder", "WriteRune"}, "Write": {"StringsBuilder", "Write"},
		"String": {"StringsBuilder", "String"}, "Len": {"StringsBuilder", "Len"},
		"Cap": {"StringsBuilder", "Cap"}, "Reset": {"StringsBuilder", "Reset"}, "Grow": {"StringsBuilder", "Grow"},
	},
	"bytes.Buffer": {
		"WriteString": {"BytesBuffer", "WriteString"}, "WriteByte": {"BytesBuffer", "WriteByte"}, "ReadString": {"BytesBuffer", "ReadString"},
		"WriteRune": {"BytesBuffer", "WriteRune"},
		"Write":     {"BytesBuffer", "Write"}, "String": {"BytesBuffer", "String"},
		"Bytes": {"BytesBuffer", "Bytes"}, "Len": {"BytesBuffer", "Len"}, "Reset": {"BytesBuffer", "Reset"},
		"Truncate": {"BytesBuffer", "Truncate"}, "Grow": {"BytesBuffer", "Grow"},
		"ReadByte": {"BytesBuffer", "ReadByte"}, "ReadRune": {"BytesBuffer", "ReadRune"}, "Next": {"BytesBuffer", "Next"},
		"WriteTo": {"BytesBuffer", "WriteTo"},
	},
	"os.File": {
		"Fd": {"Os", "File_Fd"}, "Close": {"Os", "File_Close"}, "Write": {"Os", "File_Write"}, "WriteString": {"Os", "File_WriteString"}, "Read": {"Os", "File_Read"}, "Name": {"Os", "File_Name"}, "Sync": {"Os", "File_Sync"}, "WriteAt": {"Os", "File_WriteAt"}, "ReadAt": {"Os", "File_ReadAt"}, "Seek": {"Os", "File_Seek"}, "Truncate": {"Os", "File_Truncate"}, "Stat": {"Os", "File_Stat"},
	},
	"net.IP": {
		"To4": {"Net", "IP_To4"}, "To16": {"Net", "IP_To16"}, "Equal": {"Net", "IP_Equal"}, "String": {"Net", "IP_String"},
	},
	"net.IPNet": {
		"Contains": {"Net", "IPNet_Contains"}, "String": {"Net", "IPNet_String"},
	},
	"net.OpError": {
		"Error": {"Net", "OpError_Error"}, "Unwrap": {"Net", "OpError_Unwrap"}, "Timeout": {"Net", "OpError_Timeout"}, "Temporary": {"Net", "OpError_Temporary"},
	},
	"os.SyscallError": {
		"Error": {"Os", "SyscallError_Error"}, "Unwrap": {"Os", "SyscallError_Unwrap"}, "Timeout": {"Os", "SyscallError_Timeout"},
	},
	"os.FileInfo": {
		"Name": {"Os", "FileInfo_Name"}, "Size": {"Os", "FileInfo_Size"}, "IsDir": {"Os", "FileInfo_IsDir"}, "Mode": {"Os", "FileInfo_Mode"},
	},
	"io/fs.FileInfo": {
		"Name": {"Os", "FileInfo_Name"}, "Size": {"Os", "FileInfo_Size"}, "IsDir": {"Os", "FileInfo_IsDir"}, "Mode": {"Os", "FileInfo_Mode"},
	},
	"io/fs.FileMode": {
		"Type": {"Fs", "Mode_Type"}, "IsDir": {"Fs", "Mode_IsDir"}, "IsRegular": {"Fs", "Mode_IsRegular"}, "Perm": {"Fs", "Mode_Perm"},
	},
	"crypto/sha3.SHAKE": {
		"Write": {"Crypto", "Shake_Write"}, "Read": {"Crypto", "Shake_Read"}, "Reset": {"Crypto", "Shake_Reset"},
		"Size": {"Crypto", "Shake_Size"}, "BlockSize": {"Crypto", "Shake_BlockSize"},
		"MarshalBinary": {"Crypto", "Shake_MarshalBinary"}, "UnmarshalBinary": {"Crypto", "Shake_UnmarshalBinary"},
	},
	"sync.Mutex": {
		"Lock": {"Sync", "Mutex_Lock"}, "Unlock": {"Sync", "Mutex_Unlock"}, "TryLock": {"Sync", "Mutex_TryLock"},
	},
	"sync.Cond": {
		"Wait": {"Sync", "Cond_Wait"}, "Signal": {"Sync", "Cond_Signal"}, "Broadcast": {"Sync", "Cond_Broadcast"},
	},
	"sync/atomic.Value": {
		"Load": {"Atomic", "Value_Load"}, "Store": {"Atomic", "Value_Store"}, "Swap": {"Atomic", "Value_Swap"}, "CompareAndSwap": {"Atomic", "Value_CompareAndSwap"},
	},
	"sync/atomic.Bool": {
		"Load": {"Atomic", "Bool_Load"}, "Store": {"Atomic", "Bool_Store"}, "Swap": {"Atomic", "Bool_Swap"}, "CompareAndSwap": {"Atomic", "Bool_CompareAndSwap"},
	},
	"sync/atomic.Int64": {
		"Load": {"AtomicInt", "Int_Load"}, "Store": {"AtomicInt", "Int_Store"}, "Add": {"AtomicInt", "Int_Add"}, "Swap": {"AtomicInt", "Int_Swap"}, "CompareAndSwap": {"AtomicInt", "Int_CompareAndSwap"},
	},
	"sync/atomic.Int32": {
		"Load": {"AtomicInt", "Int_Load"}, "Store": {"AtomicInt", "Int_Store"}, "Add": {"AtomicInt", "Int_Add"}, "Swap": {"AtomicInt", "Int_Swap"}, "CompareAndSwap": {"AtomicInt", "Int_CompareAndSwap"},
	},
	"sync/atomic.Uint64": {
		"Load": {"AtomicInt", "Uint_Load"}, "Store": {"AtomicInt", "Uint_Store"}, "Add": {"AtomicInt", "Uint_Add"}, "Swap": {"AtomicInt", "Uint_Swap"}, "CompareAndSwap": {"AtomicInt", "Uint_CompareAndSwap"},
	},
	"sync/atomic.Uint32": {
		"Load": {"AtomicInt", "Uint_Load"}, "Store": {"AtomicInt", "Uint_Store"}, "Add": {"AtomicInt", "Uint_Add"}, "Swap": {"AtomicInt", "Uint_Swap"}, "CompareAndSwap": {"AtomicInt", "Uint_CompareAndSwap"},
	},
	"sync/atomic.Uintptr": {
		"Load": {"AtomicInt", "Uint_Load"}, "Store": {"AtomicInt", "Uint_Store"}, "Add": {"AtomicInt", "Uint_Add"}, "Swap": {"AtomicInt", "Uint_Swap"}, "CompareAndSwap": {"AtomicInt", "Uint_CompareAndSwap"},
	},
	"sync/atomic.Pointer": {
		"Load": {"AtomicInt", "Ptr_Load"}, "Store": {"AtomicInt", "Ptr_Store"}, "Swap": {"AtomicInt", "Ptr_Swap"}, "CompareAndSwap": {"AtomicInt", "Ptr_CompareAndSwap"},
	},
	"sync.RWMutex": {
		"Lock": {"Sync", "RWMutex_Lock"}, "Unlock": {"Sync", "RWMutex_Unlock"},
		"RLock": {"Sync", "RWMutex_RLock"}, "RUnlock": {"Sync", "RWMutex_RUnlock"},
		"TryLock": {"Sync", "RWMutex_TryLock"}, "TryRLock": {"Sync", "RWMutex_TryRLock"}, "RLocker": {"Sync", "RWMutex_RLocker"},
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
	"sync.Pool": {
		"Get": {"Sync", "Pool_Get"}, "Put": {"Sync", "Pool_Put"},
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
		"Add": {"Time", "Time_Add"}, "Sub": {"Time", "Time_Sub"}, "Round": {"Time", "Time_Round"}, "Truncate": {"Time", "Time_Truncate"},
		"Before": {"Time", "Time_Before"}, "After": {"Time", "Time_After"}, "Equal": {"Time", "Time_Equal"},
		"IsZero": {"Time", "Time_IsZero"}, "UTC": {"Time", "Time_UTC"}, "Local": {"Time", "Time_Local"},
		"String": {"Time", "Time_String"}, "Format": {"Time", "Time_Format"}, "AppendFormat": {"Time", "Time_AppendFormat"},
		"Zone": {"Time", "Time_Zone"}, "YearDay": {"Time", "Time_YearDay"}, "In": {"Time", "Time_In"}, "Location": {"Time", "Time_Location"},
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
		"String": {"Reflect", "Value_String"}, "Bool": {"Reflect", "Value_Bool"}, "Bytes": {"Reflect", "Value_Bytes"},
		"Len": {"Reflect", "Value_Len"}, "Index": {"Reflect", "Value_Index"},
		"Field": {"Reflect", "Value_Field"}, "NumField": {"Reflect", "Value_NumField"},
		"IsNil": {"Reflect", "Value_IsNil"}, "IsZero": {"Reflect", "Value_IsZero"},
		"IsValid": {"Reflect", "Value_IsValid"}, "Elem": {"Reflect", "Value_Elem"}, "SetZero": {"Reflect", "Value_SetZero"},
		"MapKeys": {"Reflect", "Value_MapKeys"}, "MapIndex": {"Reflect", "Value_MapIndex"},
		"CanSet": {"Reflect", "Value_CanSet"}, "CanAddr": {"Reflect", "Value_CanAddr"},
		"SetInt": {"Reflect", "Value_SetInt"}, "SetUint": {"Reflect", "Value_SetUint"},
		"SetFloat": {"Reflect", "Value_SetFloat"}, "SetBool": {"Reflect", "Value_SetBool"},
		"SetString": {"Reflect", "Value_SetString"}, "Set": {"Reflect", "Value_Set"},
		"SetMapIndex": {"Reflect", "Value_SetMapIndex"}, "Convert": {"Reflect", "Value_Convert"},
		"Addr": {"Reflect", "Value_Addr"}, "Cap": {"Reflect", "Value_Cap"},
		"SetLen": {"Reflect", "Value_SetLen"}, "SetCap": {"Reflect", "Value_SetCap"},
		"Slice": {"Reflect", "Value_Slice"}, "Pointer": {"Reflect", "Value_Pointer"},
		"NumMethod": {"Reflect", "Value_NumMethod"}, "CanInterface": {"Reflect", "Value_CanInterface"},
		"FieldByName": {"Reflect", "Value_FieldByName"}, "FieldByIndex": {"Reflect", "Value_FieldByIndex"},
		"Method": {"Reflect", "Value_Method"}, "Call": {"Reflect", "Value_Call"},
		"MethodByName": {"Reflect", "Value_MethodByName"}, "OverflowInt": {"Reflect", "Value_OverflowInt"}, "Complex": {"Reflect", "Value_Complex"}, "OverflowUint": {"Reflect", "Value_OverflowUint"}, "OverflowFloat": {"Reflect", "Value_OverflowFloat"},
	},
}

// shimMethodExtern builds an extern descriptor for a method call on a shimmed
// stdlib type (e.g. reflect.Type.Kind), with the receiver as the first argument.
func (l *funcLowerer) shimMethodExtern(seln *types.Selection) (*goir.Extern, bool) {
	fn, ok := seln.Obj().(*types.Func)
	if !ok {
		return nil, false
	}
	// For a method promoted from an embedded shim field (struct{ *sha3.SHAKE }),
	// seln.Recv() is the outer struct, so key the registry on the method's own
	// receiver type instead. Only do this when actually promoted (index depth > 1) to
	// leave the direct-call path — and its receiver IR typing — exactly as before.
	recv := namedOf(seln.Recv())
	if len(seln.Index()) > 1 {
		if mr := namedOf(fn.Type().(*types.Signature).Recv().Type()); mr != nil {
			recv = mr
		}
	}
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

// shimExternForFunc builds the shim extern for a method belonging to a shim type,
// keyed on the method's own receiver type (used when an interface implementer
// satisfies a method through an embedded shim field, e.g. driverConn{ sync.Mutex }
// satisfying sync.Locker). Returns false if the method is not a registered shim.
func (l *funcLowerer) shimExternForFunc(fn *types.Func) (*goir.Extern, bool) {
	sig, ok := fn.Type().(*types.Signature)
	if !ok || sig.Recv() == nil {
		return nil, false
	}
	recv := namedOf(sig.Recv().Type())
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
	params := []goir.Type{goir.TObject}
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
// For a method promoted from an embedded shim field, the receiver is that field, not
// the outer value, so navigate the embedded-field path to the shim handle.
func (l *funcLowerer) shimMethodCall(e *ast.CallExpr, sel *ast.SelectorExpr, seln *types.Selection, ext *goir.Extern) goir.Type {
	if idx := seln.Index(); len(idx) > 1 {
		xt := l.exprType(sel.X)
		l.expr(sel.X)
		l.emitEmbedNav(xt, idx[:len(idx)-1], ext.Params[0])
	} else {
		l.expr(sel.X) // receiver handle
	}
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
