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
		"Floor": {"Math", "Floor"}, "Modf": {"Math", "Modf"}, "Hypot": {"Math", "Hypot"}, "Log": {"Math", "Log"},
		"Log10": {"Math", "Log10"}, "Log2": {"Math", "Log2"}, "Max": {"Math", "Max"},
		"Min": {"Math", "Min"}, "Mod": {"Math", "Mod"}, "Pow": {"Math", "Pow"}, "Pow10": {"Math", "Pow10"},
		"Remainder": {"Math", "Remainder"}, "Round": {"Math", "Round"}, "Signbit": {"Math", "Signbit"},
		"Sin": {"Math", "Sin"}, "Sinh": {"Math", "Sinh"}, "Sqrt": {"Math", "Sqrt"},
		"Tan": {"Math", "Tan"}, "Tanh": {"Math", "Tanh"}, "Trunc": {"Math", "Trunc"},
		"IsNaN": {"Math", "IsNaN"}, "IsInf": {"Math", "IsInf"}, "NaN": {"Math", "NaN"},
		"Inf":         {"Math", "Inf"},
		"Float64bits": {"Math", "Float64bits"}, "Float64frombits": {"Math", "Float64frombits"}, "Float32bits": {"Math", "Float32bits"}, "Float32frombits": {"Math", "Float32frombits"},
		"Acosh": {"Math", "Acosh"}, "Asinh": {"Math", "Asinh"}, "Atanh": {"Math", "Atanh"},
		"Expm1": {"Math", "Expm1"}, "Log1p": {"Math", "Log1p"},
		"Dim": {"Math", "Dim"}, "FMA": {"Math", "FMA"}, "Frexp": {"Math", "Frexp"}, "Ldexp": {"Math", "Ldexp"},
		"Ilogb": {"Math", "Ilogb"}, "Logb": {"Math", "Logb"}, "Nextafter": {"Math", "Nextafter"},
		"Nextafter32": {"Math", "Nextafter32"}, "RoundToEven": {"Math", "RoundToEven"}, "Sincos": {"Math", "Sincos"},
		"Gamma": {"Math", "Gamma"}, "Erf": {"Math", "Erf"}, "Erfc": {"Math", "Erfc"},
		"Erfinv": {"Math", "Erfinv"}, "Erfcinv": {"Math", "Erfcinv"}, "Lgamma": {"Math", "Lgamma"},
		"J0": {"Math", "J0"}, "J1": {"Math", "J1"}, "Jn": {"Math", "Jn"},
		"Y0": {"Math", "Y0"}, "Y1": {"Math", "Y1"}, "Yn": {"Math", "Yn"},
	},
	"go/ast": {
		"IsExported": {"Ast", "IsExported"},
	},
	"runtime": {
		"FuncForPC": {"Goruntime", "FuncForPC"}, "GOMAXPROCS": {"Goruntime", "GOMAXPROCS"}, "Caller": {"Goruntime", "Caller"}, "Stack": {"Goruntime", "Stack"},
		"Callers": {"Goruntime", "Callers"}, "CallersFrames": {"Goruntime", "CallersFrames"}, "Goexit": {"Goruntime", "Goexit"},
		"NumCPU": {"Goruntime", "NumCPU"}, "NumGoroutine": {"Goruntime", "NumGoroutine"},
		"GC": {"Goruntime", "GC"}, "Gosched": {"Goruntime", "Gosched"}, "Version": {"Goruntime", "Version"},
		"NumCgoCall": {"Goruntime", "NumCgoCall"}, "KeepAlive": {"Goruntime", "KeepAlive"}, "Breakpoint": {"Goruntime", "Breakpoint"},
		"LockOSThread": {"Goruntime", "LockOSThread"}, "UnlockOSThread": {"Goruntime", "UnlockOSThread"}, "SetFinalizer": {"Goruntime", "SetFinalizer"},
		"SetBlockProfileRate": {"Goruntime", "SetBlockProfileRate"}, "SetCPUProfileRate": {"Goruntime", "SetCPUProfileRate"},
		"SetMutexProfileFraction": {"Goruntime", "SetMutexProfileFraction"}, "GOROOT": {"Goruntime", "GOROOT"},
	},
	"flag": {
		"Lookup": {"Flag", "Lookup"}, "NewFlagSet": {"Flag", "NewFlagSet"},
		"Bool": {"Flag", "Bool"}, "Int": {"Flag", "Int"}, "Int64": {"Flag", "Int64"}, "Uint": {"Flag", "Uint"},
		"Uint64": {"Flag", "Uint64"}, "Float64": {"Flag", "Float64"}, "String": {"Flag", "String"}, "Duration": {"Flag", "Duration"},
		"BoolVar": {"Flag", "BoolVar"}, "IntVar": {"Flag", "IntVar"}, "Int64Var": {"Flag", "Int64Var"}, "UintVar": {"Flag", "UintVar"},
		"Uint64Var": {"Flag", "Uint64Var"}, "Float64Var": {"Flag", "Float64Var"}, "StringVar": {"Flag", "StringVar"}, "DurationVar": {"Flag", "DurationVar"},
		"Set": {"Flag", "Set"}, "Parsed": {"Flag", "Parsed"}, "NArg": {"Flag", "NArg"}, "NFlag": {"Flag", "NFlag"}, "Arg": {"Flag", "Arg"}, "Args": {"Flag", "Args"},
		"Visit": {"Flag", "Visit"}, "VisitAll": {"Flag", "VisitAll"}, "Func": {"Flag", "Func"}, "BoolFunc": {"Flag", "BoolFunc"},
		"Var": {"Flag", "Var"}, "SetOutput": {"Flag", "SetOutput"},
		"PrintDefaults": {"Flag", "PrintDefaults"}, "UnquoteUsage": {"Flag", "UnquoteUsage"},
	},
	"syscall": {
		"FcntlFlock": {"Syscall", "FcntlFlock"}, "Fsync": {"Syscall", "Fsync"},
		"Socket": {"Syscall", "Socket"}, "SetsockoptInt": {"Syscall", "SetsockoptInt"}, "SetNonblock": {"Syscall", "SetNonblock"},
		"CloseOnExec": {"Syscall", "CloseOnExec"}, "Close": {"Syscall", "Close"}, "Bind": {"Syscall", "Bind"}, "Listen": {"Syscall", "Listen"},
	},
	"encoding/xml": {
		"Marshal": {"Xml", "Marshal"}, "MarshalIndent": {"Xml", "MarshalIndent"}, "NewEncoder": {"Xml", "NewEncoder"},
		"Unmarshal": {"Xml", "Unmarshal"}, "NewDecoder": {"Xml", "NewDecoder"},
		"Escape": {"Xml", "Escape"}, "EscapeText": {"Xml", "EscapeText"}, "CopyToken": {"Xml", "CopyToken"},
	},
	"net/http/httputil": {
		"DumpRequest": {"Httputil", "DumpRequest"},
	},
	"errors": {
		"New": {"Errors", "New"}, "Unwrap": {"Errors", "Unwrap"}, "Is": {"Errors", "Is"}, "Join": {"Errors", "Join"},
	},
	// NOTE: "unicode" is compiled from real Go source (see compileFromSource in
	// lower.go), not shimmed — it provides RangeTable/Is/In and the full tables.
	"reflect": {
		"TypeOf": {"Reflect", "TypeOf"}, "ValueOf": {"Reflect", "ValueOf"}, "DeepEqual": {"Reflect", "DeepEqual"}, "MakeSlice": {"Reflect", "MakeSlice"}, "MakeMap": {"Reflect", "MakeMap"}, "Zero": {"Reflect", "Zero"},
		"New": {"Reflect", "New"}, "PointerTo": {"Reflect", "PointerTo"}, "PtrTo": {"Reflect", "PointerTo"},
		"MapOf": {"Reflect", "MapOf"}, "SliceOf": {"Reflect", "SliceOf"}, "ArrayOf": {"Reflect", "ArrayOf"},
		"MakeFunc": {"Reflect", "MakeFunc"}, "Copy": {"Reflect", "Copy"}, "Indirect": {"Reflect", "Indirect"},
		"Append": {"Reflect", "Append"}, "Swapper": {"Reflect", "Swapper"},
		"AppendSlice": {"Reflect", "AppendSlice"}, "MakeMapWithSize": {"Reflect", "MakeMapWithSize"},
	},
	"encoding/json": {
		"Marshal": {"Json", "Marshal"}, "MarshalIndent": {"Json", "MarshalIndent"},
		"NewDecoder": {"Json", "NewDecoder"}, "NewEncoder": {"Json", "NewEncoder"},
		"Valid": {"Json", "Valid"}, "Unmarshal": {"Json", "UnmarshalValue"},
		"Compact": {"Json", "Compact"}, "HTMLEscape": {"Json", "HTMLEscape"}, "Indent": {"Json", "Indent"},
	},
	"encoding/hex": {
		"EncodeToString": {"Hex", "EncodeToString"}, "DecodeString": {"Hex", "DecodeString"},
		"Encode": {"Hex", "Encode"}, "Decode": {"Hex", "Decode"}, "Dump": {"Hex", "Dump"},
		"EncodedLen": {"Hex", "EncodedLen"}, "DecodedLen": {"Hex", "DecodedLen"},
		"AppendEncode": {"Hex", "AppendEncode"}, "AppendDecode": {"Hex", "AppendDecode"},
		"NewDecoder": {"Hex", "NewDecoder"}, "NewEncoder": {"Hex", "NewEncoder"}, "Dumper": {"Hex", "Dumper"},
	},
	"encoding/base32": {
		"NewEncoding": {"Base32", "NewEncoding"}, "NewEncoder": {"Base32", "NewEncoder"}, "NewDecoder": {"Base32", "NewDecoder"},
	},
	"encoding/base64": {
		"NewEncoding": {"Base64", "NewEncoding"}, "NewDecoder": {"Base64", "NewDecoder"}, "NewEncoder": {"Base64", "NewEncoder"},
	},
	"crypto/sha256":   {"New": {"Crypto", "Sha256New"}, "New224": {"Crypto", "Sha224New"}, "Sum256": {"Crypto", "Sha256Sum256"}, "Sum224": {"Crypto", "Sha256Sum224"}},
	"crypto/sha1":     {"New": {"Crypto", "Sha1New"}, "Sum": {"Crypto", "Sha1Sum"}},
	"crypto/elliptic": {"P224": {"Crypto509", "P224"}, "P256": {"Crypto509", "P256"}, "P384": {"Crypto509", "P384"}, "P521": {"Crypto509", "P521"}},
	"crypto/ecdsa":    {"GenerateKey": {"Crypto509", "EcdsaGenerateKey"}, "Verify": {"CryptoSign", "EcdsaVerify"}, "Sign": {"CryptoSign", "EcdsaSign"}},
	"crypto/ed25519":  {"Verify": {"CryptoSign", "Ed25519Verify"}, "Sign": {"CryptoSign", "Ed25519Sign"}, "NewKeyFromSeed": {"CryptoSign", "Ed25519NewKeyFromSeed"}, "GenerateKey": {"CryptoSign", "Ed25519GenerateKey"}},
	"encoding/asn1":   {"Marshal": {"Asn1", "Marshal"}, "Unmarshal": {"Asn1", "Unmarshal"}},
	"encoding/pem":    {"Decode": {"Pem", "Decode"}, "EncodeToMemory": {"Pem", "EncodeToMemory"}, "Encode": {"Pem", "Encode"}},
	"crypto/rsa": {"GenerateKey": {"Crypto509", "RsaGenerateKey"},
		"VerifyPKCS1v15": {"CryptoSign", "VerifyPKCS1v15"}, "SignPKCS1v15": {"CryptoSign", "SignPKCS1v15"},
		"VerifyPSS": {"CryptoSign", "VerifyPSS"}, "SignPSS": {"CryptoSign", "SignPSS"}},
	"crypto/tls": {"Server": {"HttpTypes", "TlsServer"}, "Client": {"HttpTypes", "TlsClient"}, "X509KeyPair": {"HttpTypes", "X509KeyPair"}, "LoadX509KeyPair": {"HttpTypes", "LoadX509KeyPair"}, "NewListener": {"HttpTypes", "NewListener"}, "Listen": {"HttpTypes", "TlsListen"}, "CipherSuiteName": {"HttpTypes", "CipherSuiteName"}, "VersionName": {"HttpTypes", "VersionName"}},
	"crypto/x509": {
		"NewCertPool": {"Crypto509", "NewCertPool"},
		"CreateCertificate": {"Crypto509", "CreateCertificate"}, "ParseCertificate": {"Crypto509", "ParseCertificate"}, "ParseCertificates": {"Crypto509", "ParseCertificates"},
		"MarshalECPrivateKey": {"Crypto509", "MarshalECPrivateKey"}, "ParseECPrivateKey": {"Crypto509", "ParseECPrivateKey"},
		"MarshalPKCS1PrivateKey": {"Crypto509", "MarshalPKCS1PrivateKey"}, "ParsePKCS1PrivateKey": {"Crypto509", "ParsePKCS1PrivateKey"},
		"ParsePKCS8PrivateKey": {"Crypto509", "ParsePKCS8PrivateKey"}, "CreateCertificateRequest": {"Crypto509", "CreateCertificateRequest"},
		"ParsePKIXPublicKey": {"Crypto509", "ParsePKIXPublicKey"}, "ParsePKCS1PublicKey": {"Crypto509", "ParsePKCS1PublicKey"}, "DecryptPEMBlock": {"CryptoSign", "DecryptPEMBlock"},
		"MarshalPKIXPublicKey": {"Crypto509", "MarshalPKIXPublicKey"}, "MarshalPKCS1PublicKey": {"Crypto509", "MarshalPKCS1PublicKey"},
	},
	"crypto/sha512": {"New": {"Crypto", "Sha512New"}, "New384": {"Crypto", "Sha384New"}, "Sum512": {"Crypto", "Sha512Sum512"}, "Sum384": {"Crypto", "Sha512Sum384"},
		"New512_224": {"Crypto", "Sha512_224New"}, "New512_256": {"Crypto", "Sha512_256New"}, "Sum512_224": {"Crypto", "Sha512Sum512_224"}, "Sum512_256": {"Crypto", "Sha512Sum512_256"}},
	"crypto/md5":    {"New": {"Crypto", "Md5New"}, "Sum": {"Crypto", "Md5Sum"}},
	"crypto/sha3": {
		"New224": {"Crypto", "Sha3_224New"}, "New256": {"Crypto", "Sha3_256New"}, "New384": {"Crypto", "Sha3_384New"}, "New512": {"Crypto", "Sha3_512New"},
		"Sum224": {"Crypto", "Sha3Sum224"}, "Sum256": {"Crypto", "Sha3Sum256"}, "Sum384": {"Crypto", "Sha3Sum384"}, "Sum512": {"Crypto", "Sha3Sum512"},
		"NewSHAKE128": {"Crypto", "NewSHAKE128"}, "NewSHAKE256": {"Crypto", "NewSHAKE256"}, "NewCSHAKE128": {"Crypto", "NewCSHAKE128"}, "NewCSHAKE256": {"Crypto", "NewCSHAKE256"},
	},
	"crypto/rand":   {"Read": {"Crypto", "RandRead"}, "Int": {"Crypto", "RandInt"}, "Text": {"Crypto", "RandText"}},
	"runtime/debug": {"ReadBuildInfo": {"Debug", "ReadBuildInfo"}, "Stack": {"Debug", "Stack"}, "PrintStack": {"Debug", "PrintStack"}, "SetGCPercent": {"Debug", "SetGCPercent"}, "FreeOSMemory": {"Debug", "FreeOSMemory"}, "SetMaxStack": {"Debug", "SetMaxStack"}, "SetMaxThreads": {"Debug", "SetMaxThreads"}, "SetMemoryLimit": {"Debug", "SetMemoryLimit"}, "SetPanicOnFault": {"Debug", "SetPanicOnFault"}, "SetTraceback": {"Debug", "SetTraceback"}, "WriteHeapDump": {"Debug", "WriteHeapDump"}, "SetCrashOutput": {"Debug", "SetCrashOutput"}},
	"crypto/hmac":   {"New": {"Crypto", "HmacNew"}, "Equal": {"Crypto", "HmacEqual"}},
	"crypto/subtle": {"ConstantTimeCompare": {"Subtle", "ConstantTimeCompare"}, "ConstantTimeByteEq": {"Subtle", "ConstantTimeByteEq"}, "ConstantTimeEq": {"Subtle", "ConstantTimeEq"}, "ConstantTimeSelect": {"Subtle", "ConstantTimeSelect"}, "XORBytes": {"Subtle", "XORBytes"}, "ConstantTimeCopy": {"Subtle", "ConstantTimeCopy"}, "ConstantTimeLessOrEq": {"Subtle", "ConstantTimeLessOrEq"}, "WithDataIndependentTiming": {"Subtle", "WithDataIndependentTiming"}},
	"mime":          {"TypeByExtension": {"Mime", "TypeByExtension"}, "ParseMediaType": {"Mime", "ParseMediaType"}, "FormatMediaType": {"Mime", "FormatMediaType"}, "AddExtensionType": {"Mime", "AddExtensionType"}},
	"mime/multipart": {"NewReader": {"Multipart", "NewReader"}, "NewWriter": {"Multipart", "NewWriter"}, "FileContentDisposition": {"Multipart", "FileContentDisposition"}},
	"net/mail":      {"ParseAddress": {"Mail", "ParseAddress"}, "ParseAddressList": {"Mail", "ParseAddressList"}, "ReadMessage": {"Mail", "ReadMessage"}, "ParseDate": {"Mail", "ParseDate"}},
	"os/signal": {
		"Notify": {"Ossignal", "Notify"}, "Stop": {"Ossignal", "Stop"}, "Ignored": {"Ossignal", "Ignored"}, "NotifyContext": {"Ossignal", "NotifyContext"},
		"Reset": {"Ossignal", "Reset"}, "Ignore": {"Ossignal", "Ignore"},
	},
	"log/slog": {
		"New": {"Slog", "New"}, "NewTextHandler": {"Slog", "NewTextHandler"}, "NewJSONHandler": {"Slog", "NewJSONHandler"},
		"Default": {"Slog", "DefaultLogger"}, "SetDefault": {"Slog", "SetDefault"},
		"Info": {"Slog", "Info"}, "Debug": {"Slog", "Debug"}, "Warn": {"Slog", "Warn"}, "Error": {"Slog", "Error"},
		"String": {"Slog", "String"}, "Int": {"Slog", "Int"}, "Int64": {"Slog", "Int64"}, "Uint64": {"Slog", "Uint64"},
		"Float64": {"Slog", "Float64"}, "Bool": {"Slog", "Bool"}, "Any": {"Slog", "Any"}, "Duration": {"Slog", "Duration"},
		"Group": {"Slog", "Group"},
	},
	"net/http/cookiejar": {"New": {"Cookiejar", "New"}},
	"net/http/httptest": {
		"NewServer": {"Httptest", "NewServer"}, "NewTLSServer": {"Httptest", "NewTLSServer"},
		"NewUnstartedServer": {"Httptest", "NewUnstartedServer"},
		"NewRecorder":        {"Httptest", "NewRecorder"}, "NewRequest": {"Httptest", "NewRequest"},
	},
	"net/textproto":  {"CanonicalMIMEHeaderKey": {"Textproto", "CanonicalMIMEHeaderKey"}, "TrimString": {"Textproto", "TrimString"}, "TrimBytes": {"Textproto", "TrimBytes"}, "NewReader": {"Textproto", "NewReader"}, "NewWriter": {"Textproto", "NewWriter"}},
	"html/template":  {"New": {"Template", "NewHtml"}, "Must": {"Template", "Must"}, "IsTrue": {"Template", "IsTrueFunc"}, "JSEscapeString": {"Template", "JSEscapeString"}, "HTMLEscapeString": {"Template", "HTMLEscapeString"}, "HTMLEscaper": {"Template", "HTMLEscaper"}, "JSEscaper": {"Template", "JSEscaper"}, "URLQueryEscaper": {"Template", "URLQueryEscaper"}, "HTMLEscape": {"Template", "HTMLEscape"}, "JSEscape": {"Template", "JSEscape"}},
	"html":           {"EscapeString": {"Html", "EscapeString"}, "UnescapeString": {"Html", "UnescapeString"}},
	"text/template":  {"New": {"Template", "New"}, "Must": {"Template", "Must"}, "IsTrue": {"Template", "IsTrueFunc"}, "JSEscapeString": {"Template", "JSEscapeString"}, "HTMLEscapeString": {"Template", "HTMLEscapeString"}, "HTMLEscaper": {"Template", "HTMLEscaper"}, "JSEscaper": {"Template", "JSEscaper"}, "URLQueryEscaper": {"Template", "URLQueryEscaper"}, "HTMLEscape": {"Template", "HTMLEscape"}, "JSEscape": {"Template", "JSEscape"}},
	"os/exec":        {"Command": {"Exec", "Command"}},
	"container/list": {"New": {"List", "New"}},
	"container/heap": {
		"Init": {"Heap", "Init"}, "Push": {"Heap", "Push"}, "Pop": {"Heap", "Pop"},
		"Remove": {"Heap", "Remove"}, "Fix": {"Heap", "Fix"},
	},
	"encoding/csv":    {"NewReader": {"Csv", "NewReader"}, "NewWriter": {"Csv", "NewWriter"}},
	"encoding/binary": {"Write": {"Binary", "Write"}, "Read": {"Binary", "Read"}, "Size": {"Binary", "Size"}, "PutUvarint": {"Binary", "PutUvarint"}, "Uvarint": {"Binary", "Uvarint"}, "PutVarint": {"Binary", "PutVarint"}, "Varint": {"Binary", "Varint"}, "ReadUvarint": {"Binary", "ReadUvarint"}, "ReadVarint": {"Binary", "ReadVarint"}, "Append": {"Binary", "Append"}, "AppendUvarint": {"Binary", "AppendUvarint"}, "AppendVarint": {"Binary", "AppendVarint"}, "Encode": {"Binary", "Encode"}, "Decode": {"Binary", "Decode"}},
	"crypto/aes":      {"NewCipher": {"Aes", "NewCipher"}},
	"crypto/cipher":   {"NewGCM": {"Aes", "NewGCM"}, "NewCBCEncrypter": {"Aes", "NewCBCEncrypter"}, "NewCBCDecrypter": {"Aes", "NewCBCDecrypter"}, "NewCTR": {"Aes", "NewCTR"}, "NewCFBEncrypter": {"Aes", "NewCFBEncrypter"}, "NewCFBDecrypter": {"Aes", "NewCFBDecrypter"}, "NewOFB": {"Aes", "NewOFB"}},
	"hash/fnv":        {"New32": {"Hashes", "Fnv32"}, "New32a": {"Hashes", "Fnv32a"}, "New64": {"Hashes", "Fnv64"}, "New64a": {"Hashes", "Fnv64a"}, "New128": {"Hashes", "Fnv128"}, "New128a": {"Hashes", "Fnv128a"}},
	"hash/crc64":      {"MakeTable": {"Hashes", "Crc64MakeTable"}, "New": {"Hashes", "Crc64New"}, "Checksum": {"Hashes", "Crc64Checksum"}, "Update": {"Hashes", "Crc64Update"}},
	"index/suffixarray": {"New": {"Suffixarray", "New"}},
	"encoding/ascii85":  {"Encode": {"Ascii85", "Encode"}, "Decode": {"Ascii85", "Decode"}, "MaxEncodedLen": {"Ascii85", "MaxEncodedLen"}},
	"mime/quotedprintable": {"NewWriter": {"QuotedPrintable", "NewWriter"}, "NewReader": {"QuotedPrintable", "NewReader"}},
	"go/token": {"IsKeyword": {"GoToken", "IsKeyword"}, "IsIdentifier": {"GoToken", "IsIdentifier"}, "IsExported": {"GoToken", "IsExported"}, "Lookup": {"GoToken", "Lookup"}, "NewFileSet": {"GoToken", "NewFileSet"}},
	"text/scanner": {"TokenString": {"GoToken", "ScannerTokenString"}},
	"hash/crc32":      {"ChecksumIEEE": {"Hashes", "Crc32ChecksumIEEE"}, "Update": {"Hashes", "Crc32Update"}, "NewIEEE": {"Hashes", "Crc32NewIEEE"}, "MakeTable": {"Hashes", "Crc32MakeTable"}, "Checksum": {"Hashes", "Crc32Checksum"}, "New": {"Hashes", "Crc32New"}},
	"hash/adler32":    {"Checksum": {"Hashes", "Adler32Checksum"}, "New": {"Hashes", "Adler32New"}},
	"hash/maphash":    {"MakeSeed": {"MapHash", "MakeSeed"}, "String": {"MapHash", "StringHash"}, "Bytes": {"MapHash", "BytesHash"}},
	"compress/gzip":   {"NewWriter": {"Compress", "GzipNewWriter"}, "NewWriterLevel": {"Compress", "GzipNewWriterLevel"}, "NewReader": {"Compress", "GzipNewReader"}},
	"compress/zlib":   {"NewWriter": {"Compress", "ZlibNewWriter"}, "NewReader": {"Compress", "ZlibNewReader"}, "NewWriterLevel": {"Compress", "ZlibNewWriterLevel"}, "NewWriterLevelDict": {"Compress", "ZlibNewWriterLevelDict"}, "NewReaderDict": {"Compress", "ZlibNewReaderDict"}},
	"compress/flate":  {"NewWriter": {"Compress", "FlateNewWriter"}, "NewReader": {"Compress", "FlateNewReader"}},
	"net/url": {
		"QueryEscape": {"Url", "QueryEscape"}, "PathEscape": {"Url", "PathEscape"},
		"QueryUnescape": {"Url", "QueryUnescape"}, "PathUnescape": {"Url", "PathUnescape"},
		"Parse": {"Url", "Parse"}, "ParseRequestURI": {"Url", "ParseRequestURI"},
		"User": {"Url", "User"}, "UserPassword": {"Url", "UserPassword"}, "JoinPath": {"Url", "JoinPath"}, "ParseQuery": {"Url", "ParseQuery"},
	},
	"regexp": {
		"Compile": {"Regexp", "Compile"}, "MustCompile": {"Regexp", "MustCompile"},
		"MatchString": {"Regexp", "MatchString"}, "QuoteMeta": {"Regexp", "QuoteMeta"},
		"Match": {"Regexp", "Match"}, "MatchReader": {"Regexp", "MatchReader"},
		"CompilePOSIX": {"Regexp", "CompilePOSIX"}, "MustCompilePOSIX": {"Regexp", "MustCompilePOSIX"},
	},
	"log": {
		"New": {"Log", "New"}, "Print": {"Log", "Print"}, "Println": {"Log", "Println"}, "Printf": {"Log", "Printf"},
		"Fatal": {"Log", "Fatal"}, "Fatalf": {"Log", "Fatalf"}, "Fatalln": {"Log", "Fatalln"},
		"Panic": {"Log", "Panic"}, "Panicf": {"Log", "Panicf"}, "Panicln": {"Log", "Panicln"},
		"SetFlags": {"Log", "SetFlags"}, "SetPrefix": {"Log", "SetPrefix"}, "Flags": {"Log", "Flags"}, "Prefix": {"Log", "Prefix"},
		"Default": {"Log", "Default"}, "Output": {"Log", "Output"}, "SetOutput": {"Log", "SetOutput"}, "Writer": {"Log", "Writer"},
	},
	"math/big": {
		"NewInt": {"Big", "NewInt"}, "NewFloat": {"Big", "NewFloat"}, "Jacobi": {"Big", "Jacobi"}, "NewRat": {"Big", "NewRat"},
	},
	"path": {
		"Join": {"Path", "Join"}, "Base": {"Path", "Base"}, "Dir": {"Path", "Dir"},
		"Ext": {"Path", "Ext"}, "Clean": {"Path", "Clean"}, "Split": {"Path", "Split"}, "IsAbs": {"Path", "IsAbs"},
		"Match": {"Path", "Match"},
	},
	"path/filepath": {
		"Join": {"Path", "Join"}, "Base": {"Path", "Base"}, "Dir": {"Path", "Dir"},
		"Ext": {"Path", "Ext"}, "Clean": {"Path", "Clean"}, "Split": {"Path", "Split"}, "IsAbs": {"Path", "IsAbs"},
		"ToSlash": {"Path", "ToSlash"}, "FromSlash": {"Path", "FromSlash"}, "Walk": {"Path", "Walk"}, "Abs": {"Path", "Abs"},
		"Match": {"Path", "Match"}, "Glob": {"Path", "Glob"},
		"Rel": {"Path", "Rel"}, "SplitList": {"Path", "SplitList"}, "VolumeName": {"Path", "VolumeName"},
		"IsLocal": {"Path", "IsLocal"}, "HasPrefix": {"Path", "HasPrefix"},
	},
	"fmt": {
		"Sprint": {"Fmt", "Sprint"}, "Sprintln": {"Fmt", "Sprintln"}, "Sprintf": {"Fmt", "Sprintf"},
		"Sscanf": {"Scan", "Sscanf"}, "Sscan": {"Scan", "Sscan"}, "Sscanln": {"Scan", "Sscanln"},
		"Fscanf": {"Scan", "Fscanf"}, "Fscan": {"Scan", "Fscan"}, "Fscanln": {"Scan", "Fscanln"},
		"Print": {"Fmt", "Print"}, "Println": {"Fmt", "Println"}, "Printf": {"Fmt", "Printf"},
		"Errorf": {"Fmt", "Errorf"},
		"Fprint": {"Fmt", "Fprint"}, "Fprintln": {"Fmt", "Fprintln"}, "Fprintf": {"Fmt", "Fprintf"},
	},
	"io": {
		"WriteString": {"Io", "WriteString"}, "ReadAll": {"Readers", "ReadAll"}, "Copy": {"Readers", "Copy"},
		"ReadFull": {"Io", "ReadFull"}, "NopCloser": {"Io", "NopCloser"}, "LimitReader": {"Readers", "LimitReader"},
	},
	"bufio": {
		"NewScanner": {"Bufio", "NewScanner"}, "NewWriter": {"Bufio", "NewWriter"}, "NewWriterSize": {"Bufio", "NewWriterSize"},
		"NewReader": {"Bufio", "NewReader"}, "NewReaderSize": {"Bufio", "NewReaderSize"},
		// SplitFunc values: used as a value passed to Scanner.Split, each lowers to a
		// closure returning the split mode marker (see Scanner_Split).
		"ScanLines": {"Bufio", "ScanLinesMarker"}, "ScanWords": {"Bufio", "ScanWordsMarker"}, "ScanRunes": {"Bufio", "ScanRunesMarker"}, "ScanBytes": {"Bufio", "ScanBytesMarker"},
		"NewReadWriter": {"Bufio", "NewReadWriter"},
	},
	"io/fs": {
		"Stat": {"Fs", "Stat"}, "Sub": {"Fs", "Sub"}, "ValidPath": {"Fs", "ValidPath"}, "ReadDir": {"Fs", "ReadDir"},
		"FormatDirEntry": {"Fs", "FormatDirEntry"},
	},
	"net/netip": {
		"AddrFrom4": {"Netip", "AddrFrom4"}, "AddrFrom16": {"Netip", "AddrFrom16"}, "AddrFromSlice": {"Netip", "AddrFromSlice"},
		"IPv4Unspecified": {"Netip", "IPv4Unspecified"}, "IPv6Unspecified": {"Netip", "IPv6Unspecified"}, "IPv6Loopback": {"Netip", "IPv6Loopback"},
		"IPv6LinkLocalAllNodes": {"Netip", "IPv6LinkLocalAllNodes"}, "IPv6LinkLocalAllRouters": {"Netip", "IPv6LinkLocalAllRouters"},
		"ParseAddr": {"Netip", "ParseAddr"}, "MustParseAddr": {"Netip", "MustParseAddr"},
		"AddrPortFrom": {"Netip", "AddrPortFrom"}, "ParseAddrPort": {"Netip", "ParseAddrPort"}, "MustParseAddrPort": {"Netip", "MustParseAddrPort"},
		"PrefixFrom": {"Netip", "PrefixFrom"}, "ParsePrefix": {"Netip", "ParsePrefix"}, "MustParsePrefix": {"Netip", "MustParsePrefix"},
	},
	"net": {
		"Listen": {"Net", "Listen"}, "Dial": {"Net", "Dial"}, "FileListener": {"Net", "FileListener"},
		"InterfaceByName": {"Net", "InterfaceByName"},
		"ParseIP": {"Net", "ParseIP"}, "ParseMAC": {"Net", "ParseMAC"}, "ParseCIDR": {"Net", "ParseCIDR"},
		"SplitHostPort": {"Net", "SplitHostPort"}, "JoinHostPort": {"Net", "JoinHostPort"},
		"ResolveTCPAddr": {"Net", "ResolveTCPAddr"}, "ResolveUDPAddr": {"Net", "ResolveUDPAddr"},
		"TCPAddrFromAddrPort": {"Net", "TCPAddrFromAddrPort"}, "UDPAddrFromAddrPort": {"Net", "UDPAddrFromAddrPort"},
		"ResolveIPAddr": {"Net", "ResolveIPAddr"}, "ResolveUnixAddr": {"Net", "ResolveUnixAddr"},
		"ListenUDP": {"Net", "ListenUDP"}, "DialUDP": {"Net", "DialUDP"},
		"Interfaces": {"Net", "Interfaces"},
		"IPv4": {"Net", "IPv4"}, "IPv4Mask": {"Net", "IPv4Mask"}, "CIDRMask": {"Net", "CIDRMask"},
	},
	"net/http": {
		"Get": {"Http", "Get"}, "Post": {"Http", "Post"}, "ReadResponse": {"Http", "ReadResponse"},
		"HandleFunc": {"Http", "HandleFunc"}, "ListenAndServe": {"Http", "ListenAndServe"}, "Redirect": {"Http", "Redirect"}, "NewServeMux": {"HttpTypes", "NewServeMux"},
		"CanonicalHeaderKey": {"Http", "CanonicalHeaderKey"}, "StatusText": {"Http", "StatusText"}, "DetectContentType": {"Http", "DetectContentType"}, "Error": {"Http", "Error"}, "NotFound": {"Http", "NotFound"}, "NotFoundHandler": {"Http", "NotFoundHandler"}, "RedirectHandler": {"Http", "RedirectHandler"},
		"NewResponseController": {"Http", "NewResponseController"}, "SetCookie": {"Http", "SetCookie"},
		"ParseHTTPVersion": {"Http", "ParseHTTPVersion"}, "ParseCookie": {"Http", "ParseCookie"}, "ParseSetCookie": {"Http", "ParseSetCookie"},
		"ServeFile": {"Http", "ServeFile"}, "ServeContent": {"Http", "ServeContent"}, "FileServer": {"Http", "FileServer"}, "StripPrefix": {"Http", "StripPrefix"}, "Serve": {"Http", "Serve"}, "ListenAndServeTLS": {"Http", "ListenAndServeTLS"},
		"NewRequest": {"Http", "NewRequest"}, "NewRequestWithContext": {"Http", "NewRequestWithContext"}, "ParseTime": {"Http", "ParseTime"},
	},
	"math/rand/v2": {
		"IntN": {"Rand2", "IntN"}, "Int64N": {"Rand2", "Int64N"}, "Int32N": {"Rand2", "Int32N"}, "UintN": {"Rand2", "UintN"},
		"Int": {"Rand2", "Int"}, "Int64": {"Rand2", "Int64"}, "Int32": {"Rand2", "Int32"}, "Uint64": {"Rand2", "Uint64"}, "Uint32": {"Rand2", "Uint32"},
		"Float64": {"Rand2", "Float64"}, "Float32": {"Rand2", "Float32"}, "Shuffle": {"Rand2", "Shuffle"}, "Perm": {"Rand2", "Perm"},
		"NewPCG": {"Rand2", "NewPCG"}, "New": {"Rand2", "NewV2"}, "NewChaCha8": {"Rand2", "NewChaCha8"},
	},
	"math/rand": {
		"NewSource": {"Rand", "NewSource"}, "New": {"Rand", "New"},
		"Float64": {"Rand", "Float64"}, "Int63": {"Rand", "Int63"}, "Int": {"Rand", "Int"},
		"Int63n": {"Rand", "Int63n"}, "Intn": {"Rand", "Intn"}, "Perm": {"Rand", "Perm"}, "Seed": {"Rand", "Seed"},
		"Uint64": {"Rand", "Uint64"}, "Uint32": {"Rand", "Uint32"}, "Int31": {"Rand", "Int31"}, "Read": {"Rand", "Read"},
		"Shuffle": {"Rand", "Shuffle"}, "Int31n": {"Rand", "Int31n"}, "Float32": {"Rand", "Float32"},
		"NormFloat64": {"Rand", "NormFloat64"}, "ExpFloat64": {"Rand", "ExpFloat64"}, "NewZipf": {"Rand", "NewZipf"},
	},
	"sync": {"NewCond": {"Sync", "NewCond"}, "OnceFunc": {"Sync", "OnceFunc"}, "OnceValue": {"Sync", "OnceValue"}, "OnceValues": {"Sync", "OnceValues"}},
	"sync/atomic": {
		"AddInt64": {"Atomic", "AddInt64"}, "AddInt32": {"Atomic", "AddInt32"}, "AddUint64": {"Atomic", "AddUint64"},
		"LoadInt64": {"Atomic", "LoadInt64"}, "LoadInt32": {"Atomic", "LoadInt32"}, "LoadUint64": {"Atomic", "LoadUint64"},
		"StoreInt64": {"Atomic", "StoreInt64"}, "StoreInt32": {"Atomic", "StoreInt32"}, "StoreUint64": {"Atomic", "StoreUint64"},
		"LoadUint32": {"Atomic", "LoadUint32"}, "StoreUint32": {"Atomic", "StoreUint32"},
		"AddUint32": {"Atomic", "AddUint32"}, "SwapUint64": {"Atomic", "SwapUint64"},
		"CompareAndSwapUint64": {"Atomic", "CompareAndSwapUint64"}, "CompareAndSwapUint32": {"Atomic", "CompareAndSwapUint32"},
		"SwapInt64": {"Atomic", "SwapInt64"}, "SwapInt32": {"Atomic", "SwapInt32"}, "SwapUint32": {"Atomic", "SwapUint32"},
		"CompareAndSwapInt64": {"Atomic", "CompareAndSwapInt64"}, "CompareAndSwapInt32": {"Atomic", "CompareAndSwapInt32"},
		"AndInt32": {"Atomic", "AndInt32"}, "OrInt32": {"Atomic", "OrInt32"}, "AndInt64": {"Atomic", "AndInt64"}, "OrInt64": {"Atomic", "OrInt64"},
		"AndUint32": {"Atomic", "AndUint32"}, "OrUint32": {"Atomic", "OrUint32"}, "AndUint64": {"Atomic", "AndUint64"}, "OrUint64": {"Atomic", "OrUint64"},
		"AddUintptr": {"Atomic", "AddUintptr"}, "LoadUintptr": {"Atomic", "LoadUintptr"}, "StoreUintptr": {"Atomic", "StoreUintptr"},
		"SwapUintptr": {"Atomic", "SwapUintptr"}, "CompareAndSwapUintptr": {"Atomic", "CompareAndSwapUintptr"},
		"AndUintptr": {"Atomic", "AndUintptr"}, "OrUintptr": {"Atomic", "OrUintptr"},
		"LoadPointer": {"Atomic", "LoadPointer"}, "StorePointer": {"Atomic", "StorePointer"},
		"SwapPointer": {"Atomic", "SwapPointer"}, "CompareAndSwapPointer": {"Atomic", "CompareAndSwapPointer"},
	},
	"context": {
		"Background": {"Context", "Background"}, "TODO": {"Context", "TODO"},
		"WithValue": {"Context", "WithValue"}, "WithCancel": {"Context", "WithCancel"},
		"WithTimeout": {"Context", "WithTimeout"}, "WithDeadline": {"Context", "WithDeadline"}, "WithCancelCause": {"Context", "WithCancelCause"},
		"Cause": {"Context", "Cause"},
		"WithTimeoutCause": {"Context", "WithTimeoutCause"}, "WithDeadlineCause": {"Context", "WithDeadlineCause"},
		"WithoutCancel": {"Context", "WithoutCancel"}, "AfterFunc": {"Context", "AfterFunc"},
	},
	"sort": {
		"Ints": {"Sort", "Ints"}, "Float64s": {"Sort", "Float64s"}, "Strings": {"Sort", "Strings"},
		"IntsAreSorted": {"Sort", "IntsAreSorted"}, "SearchInts": {"Sort", "SearchInts"},
		"Float64sAreSorted": {"Sort", "Float64sAreSorted"}, "StringsAreSorted": {"Sort", "StringsAreSorted"},
		"SearchStrings": {"Sort", "SearchStrings"}, "SearchFloat64s": {"Sort", "SearchFloat64s"},
		"Search": {"Sort", "Search"}, "Slice": {"Sort", "Slice"}, "SliceStable": {"Sort", "SliceStable"}, "SliceIsSorted": {"Sort", "SliceIsSorted"},
	},
	// slices: the functions returning a concrete type (bool/int/index+found/void). Functions
	// returning a type parameter (Max/Min/Clone/Compact/Concat) are deferred — the backend
	// does not yet unbox a shim's generic-typed return.
	"slices": {
		"Sort": {"Slices", "Sort"}, "SortFunc": {"Slices", "SortFunc"}, "SortStableFunc": {"Slices", "SortStableFunc"},
		"Contains": {"Slices", "Contains"}, "ContainsFunc": {"Slices", "ContainsFunc"},
		"Index": {"Slices", "Index"}, "IndexFunc": {"Slices", "IndexFunc"},
		"Max": {"Slices", "Max"}, "Min": {"Slices", "Min"}, "MaxFunc": {"Slices", "MaxFunc"}, "MinFunc": {"Slices", "MinFunc"},
		"Equal": {"Slices", "Equal"}, "EqualFunc": {"Slices", "EqualFunc"},
		"Reverse": {"Slices", "Reverse"}, "IsSorted": {"Slices", "IsSorted"}, "IsSortedFunc": {"Slices", "IsSortedFunc"},
		"BinarySearch": {"Slices", "BinarySearch"}, "BinarySearchFunc": {"Slices", "BinarySearchFunc"},
		"Clone": {"Slices", "Clone"}, "Compact": {"Slices", "Compact"}, "CompactFunc": {"Slices", "CompactFunc"}, "Concat": {"Slices", "Concat"},
		"Insert": {"Slices", "Insert"}, "Delete": {"Slices", "Delete"}, "Replace": {"Slices", "Replace"}, "DeleteFunc": {"Slices", "DeleteFunc"},
		"Repeat": {"Slices", "Repeat"}, "Compare": {"Slices", "Compare"}, "CompareFunc": {"Slices", "CompareFunc"},
		"Values": {"Slices", "Values"}, "All": {"Slices", "All"}, "Backward": {"Slices", "Backward"},
		"Collect": {"Slices", "Collect"}, "Sorted": {"Slices", "Sorted"}, "SortedFunc": {"Slices", "SortedFunc"},
	},
	"cmp": {
		"Compare": {"Cmp", "Compare"}, "Less": {"Cmp", "Less"}, "Or": {"Cmp", "Or"},
	},
	// maps: the non-iterator functions only (Keys/Values/All return iter.Seq, unsupported).
	"maps": {
		"Clone": {"Maps", "Clone"}, "Copy": {"Maps", "Copy"}, "Equal": {"Maps", "Equal"},
		"EqualFunc": {"Maps", "EqualFunc"}, "DeleteFunc": {"Maps", "DeleteFunc"},
		"Keys": {"Maps", "Keys"}, "Values": {"Maps", "Values"}, "All": {"Maps", "All"},
	},
	"time": {
		"Sleep": {"Time", "Sleep"}, "After": {"Time", "After"},
		"Now": {"Time", "Now"}, "Unix": {"Time", "Unix"}, "UnixMilli": {"Time", "UnixMilli"}, "UnixMicro": {"Time", "UnixMicro"}, "Date": {"Time", "Date"}, "Since": {"Time", "Since"}, "Until": {"Time", "Until"},
		"FixedZone": {"Time", "FixedZone"}, "NewTicker": {"Time", "NewTicker"}, "NewTimer": {"Time", "NewTimer"},
		"Parse": {"Time", "Parse"}, "LoadLocation": {"Time", "LoadLocation"}, "ParseDuration": {"Time", "ParseDuration"}, "ParseInLocation": {"Time", "ParseInLocation"},
		"Tick": {"Time", "Tick"}, "AfterFunc": {"Time", "AfterFunc"},
	},
	"math/cmplx": {
		"Abs": {"Cmplx", "Abs"}, "Conj": {"Cmplx", "Conj"}, "Phase": {"Cmplx", "Phase"}, "Polar": {"Cmplx", "Polar"},
		"Inf": {"Cmplx", "Inf"}, "NaN": {"Cmplx", "NaN"}, "IsInf": {"Cmplx", "IsInf"},
		"IsNaN": {"Cmplx", "IsNaN"}, "Sqrt": {"Cmplx", "Sqrt"}, "Log": {"Cmplx", "Log"},
		"Log10": {"Cmplx", "Log10"}, "Rect": {"Cmplx", "Rect"}, "Exp": {"Cmplx", "Exp"},
		"Pow": {"Cmplx", "Pow"}, "Cot": {"Cmplx", "Cot"},
		"Sin": {"Cmplx", "Sin"}, "Cos": {"Cmplx", "Cos"}, "Tan": {"Cmplx", "Tan"},
		"Sinh": {"Cmplx", "Sinh"}, "Cosh": {"Cmplx", "Cosh"}, "Tanh": {"Cmplx", "Tanh"},
		"Asin": {"Cmplx", "Asin"}, "Acos": {"Cmplx", "Acos"}, "Atan": {"Cmplx", "Atan"},
		"Asinh": {"Cmplx", "Asinh"}, "Acosh": {"Cmplx", "Acosh"}, "Atanh": {"Cmplx", "Atanh"},
		// Exp/Rect and the trig/Pow functions call Sin/Cos/Exp/Log, which match Go to within
		// the last ULP on this platform (math.Sin/Cos already do) — see LIMITATIONS.
	},
	"math/bits": {
		"OnesCount": {"MathBits", "OnesCount"}, "OnesCount64": {"MathBits", "OnesCount64"}, "OnesCount32": {"MathBits", "OnesCount32"},
		"LeadingZeros": {"MathBits", "LeadingZeros"}, "LeadingZeros64": {"MathBits", "LeadingZeros64"},
		"TrailingZeros": {"MathBits", "TrailingZeros"}, "TrailingZeros64": {"MathBits", "TrailingZeros64"},
		"Len": {"MathBits", "Len"}, "Len64": {"MathBits", "Len64"}, "RotateLeft64": {"MathBits", "RotateLeft64"},
		"Reverse64": {"MathBits", "Reverse64"}, "ReverseBytes64": {"MathBits", "ReverseBytes64"},
		"Reverse": {"MathBits", "Reverse"}, "ReverseBytes": {"MathBits", "ReverseBytes"}, "RotateLeft": {"MathBits", "RotateLeft"},
		"Add": {"MathBits", "Add"}, "Add32": {"MathBits", "Add32"}, "Add64": {"MathBits", "Add64"},
		"Sub": {"MathBits", "Sub"}, "Sub32": {"MathBits", "Sub32"}, "Sub64": {"MathBits", "Sub64"},
		"Mul": {"MathBits", "Mul"}, "Mul32": {"MathBits", "Mul32"}, "Mul64": {"MathBits", "Mul64"},
		"Div": {"MathBits", "Div"}, "Div32": {"MathBits", "Div32"}, "Div64": {"MathBits", "Div64"},
		"Rem": {"MathBits", "Rem"}, "Rem32": {"MathBits", "Rem32"}, "Rem64": {"MathBits", "Rem64"},
		"OnesCount8": {"MathBits", "OnesCount8"}, "OnesCount16": {"MathBits", "OnesCount16"},
		"LeadingZeros8": {"MathBits", "LeadingZeros8"}, "LeadingZeros16": {"MathBits", "LeadingZeros16"}, "LeadingZeros32": {"MathBits", "LeadingZeros32"},
		"TrailingZeros8": {"MathBits", "TrailingZeros8"}, "TrailingZeros16": {"MathBits", "TrailingZeros16"}, "TrailingZeros32": {"MathBits", "TrailingZeros32"},
		"Len8": {"MathBits", "Len8"}, "Len16": {"MathBits", "Len16"}, "Len32": {"MathBits", "Len32"},
		"RotateLeft8": {"MathBits", "RotateLeft8"}, "RotateLeft16": {"MathBits", "RotateLeft16"}, "RotateLeft32": {"MathBits", "RotateLeft32"},
		"ReverseBytes16": {"MathBits", "ReverseBytes16"}, "ReverseBytes32": {"MathBits", "ReverseBytes32"}, "Reverse32": {"MathBits", "Reverse32"},
		"Reverse16": {"MathBits", "Reverse16"}, "Reverse8": {"MathBits", "Reverse8"},
	},
	"os": {
		"Getenv": {"Os", "Getenv"}, "LookupEnv": {"Os", "LookupEnv"}, "Setenv": {"Os", "Setenv"}, "Getwd": {"Os", "Getwd"}, "Environ": {"Os", "Environ"}, "FindProcess": {"Os", "FindProcess"}, "DirFS": {"Os", "DirFS"},
		"Hostname": {"Os", "Hostname"}, "IsPermission": {"Os", "IsPermission"}, "NewSyscallError": {"Os", "NewSyscallError"}, "Expand": {"Os", "Expand"}, "ExpandEnv": {"Os", "ExpandEnv"},
		"Unsetenv": {"Os", "Unsetenv"}, "Exit": {"Os", "Exit"}, "Getpid": {"Os", "Getpid"},
		"Getuid": {"Os", "Getuid"}, "Getgid": {"Os", "Getgid"}, "Getppid": {"Os", "Getppid"},
		"ReadFile": {"Os", "ReadFile"}, "WriteFile": {"Os", "WriteFile"}, "Open": {"Os", "Open"},
		"Create": {"Os", "Create"}, "OpenFile": {"Os", "OpenFile"}, "Remove": {"Os", "Remove"}, "RemoveAll": {"Os", "RemoveAll"}, "Rename": {"Os", "Rename"}, "UserCacheDir": {"Os", "UserCacheDir"}, "UserConfigDir": {"Os", "UserConfigDir"}, "UserHomeDir": {"Os", "UserHomeDir"}, "NewFile": {"Os", "NewFile"}, "CreateTemp": {"Os", "CreateTemp"}, "MkdirTemp": {"Os", "MkdirTemp"}, "TempDir": {"Os", "TempDir"},
		"Stat": {"Os", "Stat"}, "Lstat": {"Os", "Lstat"}, "IsNotExist": {"Os", "IsNotExist"}, "MkdirAll": {"Os", "MkdirAll"}, "Mkdir": {"Os", "Mkdir"}, "Chtimes": {"Os", "Chtimes"}, "Chmod": {"Os", "Chmod"},
		"IsExist": {"Os", "IsExist"}, "IsTimeout": {"Os", "IsTimeout"}, "IsPathSeparator": {"Os", "IsPathSeparator"}, "Getpagesize": {"Os", "Getpagesize"}, "Clearenv": {"Os", "Clearenv"},
		"Chdir": {"Os", "Chdir"}, "Truncate": {"Os", "Truncate"}, "Symlink": {"Os", "Symlink"}, "Readlink": {"Os", "Readlink"}, "ReadDir": {"Os", "ReadDir"},
	},
	"bytes": {
		"Equal": {"Bytes", "Equal"}, "EqualFold": {"Bytes", "EqualFold"}, "Compare": {"Bytes", "Compare"}, "Contains": {"Bytes", "Contains"},
		"HasPrefix": {"Bytes", "HasPrefix"}, "HasSuffix": {"Bytes", "HasSuffix"}, "Index": {"Bytes", "Index"},
		"LastIndex": {"Bytes", "LastIndex"}, "LastIndexByte": {"Bytes", "LastIndexByte"}, "Replace": {"Bytes", "Replace"}, "ReplaceAll": {"Bytes", "ReplaceAll"}, "Clone": {"Bytes", "Clone"},
		"IndexByte": {"Bytes", "IndexByte"}, "IndexRune": {"Bytes", "IndexRune"}, "IndexAny": {"Bytes", "IndexAny"}, "Runes": {"Bytes", "Runes"}, "Count": {"Bytes", "Count"}, "ToUpper": {"Bytes", "ToUpper"},
		"ToLower": {"Bytes", "ToLower"}, "TrimSpace": {"Bytes", "TrimSpace"}, "Trim": {"Bytes", "Trim"}, "TrimPrefix": {"Bytes", "TrimPrefix"}, "TrimSuffix": {"Bytes", "TrimSuffix"}, "Repeat": {"Bytes", "Repeat"},
		"Split": {"Bytes", "Split"}, "SplitAfterN": {"Bytes", "SplitAfterN"}, "Join": {"Bytes", "Join"},
		"NewReader": {"Readers", "NewBytesReader"}, "NewBuffer": {"BytesBuffer", "NewBuffer"}, "NewBufferString": {"BytesBuffer", "NewBufferString"},
		"ContainsAny": {"Bytes", "ContainsAny"}, "ContainsRune": {"Bytes", "ContainsRune"}, "ContainsFunc": {"Bytes", "ContainsFunc"},
		"IndexFunc": {"Bytes", "IndexFunc"}, "LastIndexAny": {"Bytes", "LastIndexAny"}, "LastIndexFunc": {"Bytes", "LastIndexFunc"},
		"Cut": {"Bytes", "Cut"}, "CutPrefix": {"Bytes", "CutPrefix"}, "CutSuffix": {"Bytes", "CutSuffix"},
		"Fields": {"Bytes", "Fields"}, "FieldsFunc": {"Bytes", "FieldsFunc"}, "SplitN": {"Bytes", "SplitN"}, "SplitAfter": {"Bytes", "SplitAfter"},
		"Map": {"Bytes", "Map"}, "Title": {"Bytes", "Title"}, "ToTitle": {"Bytes", "ToTitle"}, "ToValidUTF8": {"Bytes", "ToValidUTF8"},
		"TrimLeft": {"Bytes", "TrimLeft"}, "TrimRight": {"Bytes", "TrimRight"}, "TrimFunc": {"Bytes", "TrimFunc"}, "TrimLeftFunc": {"Bytes", "TrimLeftFunc"}, "TrimRightFunc": {"Bytes", "TrimRightFunc"},
		"Lines": {"Bytes", "Lines"}, "SplitSeq": {"Bytes", "SplitSeq"}, "SplitAfterSeq": {"Bytes", "SplitAfterSeq"}, "FieldsSeq": {"Bytes", "FieldsSeq"}, "FieldsFuncSeq": {"Bytes", "FieldsFuncSeq"},
	},
	"strconv": {
		"IsPrint": {"Strconv", "IsPrint"}, "IsGraphic": {"Strconv", "IsGraphic"},
		"Itoa": {"Strconv", "Itoa"}, "Atoi": {"Strconv", "Atoi"},
		"FormatInt": {"Strconv", "FormatInt"}, "FormatUint": {"Strconv", "FormatUint"},
		"FormatBool": {"Strconv", "FormatBool"}, "FormatFloat": {"Strconv", "FormatFloat"},
		"ParseInt": {"Strconv", "ParseInt"}, "ParseUint": {"Strconv", "ParseUint"},
		"ParseFloat": {"Strconv", "ParseFloat"}, "ParseBool": {"Strconv", "ParseBool"},
		"Quote": {"Strconv", "Quote"}, "QuoteToASCII": {"Strconv", "QuoteToASCII"},
		"CanBackquote": {"Strconv", "CanBackquote"}, "AppendInt": {"Strconv", "AppendInt"}, "AppendUint": {"Strconv", "AppendUint"}, "AppendBool": {"Strconv", "AppendBool"}, "AppendFloat": {"Strconv", "AppendFloat"}, "AppendQuote": {"Strconv", "AppendQuote"},
		"QuoteRune": {"Strconv", "QuoteRune"}, "QuoteRuneToASCII": {"Strconv", "QuoteRuneToASCII"}, "QuoteRuneToGraphic": {"Strconv", "QuoteRuneToGraphic"}, "QuoteToGraphic": {"Strconv", "QuoteToGraphic"},
		"AppendQuoteToASCII": {"Strconv", "AppendQuoteToASCII"}, "AppendQuoteToGraphic": {"Strconv", "AppendQuoteToGraphic"},
		"AppendQuoteRune": {"Strconv", "AppendQuoteRune"}, "AppendQuoteRuneToASCII": {"Strconv", "AppendQuoteRuneToASCII"}, "AppendQuoteRuneToGraphic": {"Strconv", "AppendQuoteRuneToGraphic"},
		"Unquote": {"Strconv", "Unquote"}, "UnquoteChar": {"Strconv", "UnquoteChar"}, "QuotedPrefix": {"Strconv", "QuotedPrefix"},
		"FormatComplex": {"Strconv", "FormatComplex"}, "ParseComplex": {"Strconv", "ParseComplex"},
	},
	"unicode/utf8": {
		"RuneCountInString": {"Utf8", "RuneCountInString"}, "RuneCount": {"Utf8", "RuneCount"},
		"ValidString": {"Utf8", "ValidString"}, "ValidRune": {"Utf8", "ValidRune"}, "RuneLen": {"Utf8", "RuneLen"},
		"Valid": {"Utf8", "Valid"}, "EncodeRune": {"Utf8", "EncodeRune"},
		"DecodeRuneInString": {"Utf8", "DecodeRuneInString"}, "DecodeRune": {"Utf8", "DecodeRune"},
		"DecodeLastRuneInString": {"Utf8", "DecodeLastRuneInString"}, "DecodeLastRune": {"Utf8", "DecodeLastRune"}, "FullRune": {"Utf8", "FullRune"}, "FullRuneInString": {"Utf8", "FullRuneInString"}, "RuneStart": {"Utf8", "RuneStart"},
		"AppendRune": {"Utf8", "AppendRune"},
	},
	"unicode/utf16": {
		"EncodeRune": {"Utf16", "EncodeRune"}, "DecodeRune": {"Utf16", "DecodeRune"},
		"IsSurrogate": {"Utf16", "IsSurrogate"}, "Encode": {"Utf16", "Encode"}, "Decode": {"Utf16", "Decode"},
		"RuneLen": {"Utf16", "RuneLen"}, "AppendRune": {"Utf16", "AppendRune"},
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
		"Clone": {"Strings", "Clone"}, "ContainsFunc": {"Strings", "ContainsFunc"},
		"CutPrefix": {"Strings", "CutPrefix"}, "CutSuffix": {"Strings", "CutSuffix"},
		"LastIndexAny": {"Strings", "LastIndexAny"}, "LastIndexFunc": {"Strings", "LastIndexFunc"},
		"ToValidUTF8": {"Strings", "ToValidUTF8"},
		"Lines": {"Strings", "Lines"}, "SplitSeq": {"Strings", "SplitSeq"}, "SplitAfterSeq": {"Strings", "SplitAfterSeq"},
		"FieldsSeq": {"Strings", "FieldsSeq"}, "FieldsFuncSeq": {"Strings", "FieldsFuncSeq"},
	},
}

// opaqueShimTypes are stdlib types represented at runtime as opaque object
// handles (not lowered structures); method calls on them dispatch to shims.
var opaqueShimTypes = map[string]bool{
	"reflect.Type":                   true,
	"reflect.Value":                  true,
	"reflect.SliceHeader":            true,
	"reflect.StringHeader":           true,
	"sync.Mutex":                     true,
	"sync.Cond":                      true,
	"sync/atomic.Value":              true,
	"sync/atomic.Bool":               true,
	"sync/atomic.Int64":              true,
	"sync/atomic.Int32":              true,
	"sync/atomic.Uint64":             true,
	"sync/atomic.Uint32":             true,
	"sync/atomic.Uintptr":            true,
	"sync/atomic.Pointer":            true,
	"sync.RWMutex":                   true,
	"sync.WaitGroup":                 true,
	"sync.Once":                      true,
	"sync.Map":                       true,
	"sync.Pool":                      true,
	"strconv.NumError":               true,
	"strings.Builder":                true,
	"strings.Replacer":               true,
	"bytes.Buffer":                   true,
	"os.File":                        true,
	"os.FileInfo":                    true,
	"crypto/sha3.SHAKE":              true,
	"crypto/sha3.SHA3":               true,
	"io/fs.FileInfo":                 true,
	"io/fs.DirEntry":                 true,
	"io/fs.PathError":                true,
	"os.PathError":                   true,
	"os.LinkError":                   true,
	"text/template.ExecError":        true,
	"net/http.ProtocolError":          true,
	"net/http.MaxBytesError":          true,
	"html/template.Error":            true,
	"runtime.Frame":                  true,
	"runtime.Frames":                 true,
	"time.Time":                      true,
	"time.Location":                  true,
	"math/rand.Rand":                 true,
	"math/rand.Source":               true,
	"math/rand.Zipf":                 true,
	"math/rand/v2.Rand":              true,
	"math/rand/v2.PCG":               true,
	"math/rand/v2.ChaCha8":           true,
	"encoding/base64.Encoding":       true,
	"encoding/binary.littleEndian":   true,
	"encoding/binary.bigEndian":      true,
	"encoding/binary.ByteOrder":      true,
	"regexp.Regexp":                  true,
	"net/url.URL":                    true,
	"net/url.Userinfo":               true,
	"net/url.Error":                  true,
	"net/http.Response":              true,
	"net.Listener":                   true,
	"net.Conn":                       true,
	"context.Context":                true,
	"io.ReadCloser":                  true,
	"net.IPNet":                      true,
	"net.OpError":                    true,
	"encoding/xml.Encoder":           true,
	"encoding/xml.Decoder":           true,
	"encoding/xml.Name":              true,
	"encoding/xml.StartElement":      true,
	"encoding/xml.EndElement":        true,
	"encoding/xml.Attr":              true,
	"os.SyscallError":                true,
	"syscall.Flock_t":                true,
	"syscall.SockaddrInet4":          true,
	"syscall.SockaddrInet6":          true,
	"net/mail.Address":               true,
	"net/mail.Message":               true,
	"net/textproto.Reader":           true,
	"net/textproto.Writer":           true,
	"net/textproto.Error":            true,
	"net/textproto.dotWriter":        true,
	"go/token.Position":               true,
	"encoding/csv.ParseError":          true,
	"time.ParseError":                  true,
	"encoding/hex.encoder":             true,
	"encoding/hex.dumper":              true,
	"encoding/base64.encoder":          true,
	"text/scanner.Position":            true,
	"text/scanner.Scanner":             true,
	"go/token.FileSet":                true,
	"go/token.File":                   true,
	"encoding/asn1.StructuralError":  true,
	"encoding/asn1.SyntaxError":      true,
	"encoding/asn1.BitString":       true,
	"html/template.Template":         true,
	"text/template.Template":         true,
	"net/netip.Addr":                 true,
	"net/netip.AddrPort":             true,
	"net/netip.Prefix":               true,
	"flag.FlagSet":                   true,
	"flag.Flag":                      true,
	"flag.Value":                     true,
	"mime.WordDecoder":               true,
	"index/suffixarray.Index":         true,
	"mime/quotedprintable.Writer":     true,
	"mime/quotedprintable.Reader":     true,
	"net.TCPAddr":                    true,
	"net.UDPAddr":                    true,
	"net.Dialer":                     true,
	"net.Resolver":                   true,
	"net.Interface":                  true,
	"net.IPAddr":                     true,
	"net.UnixAddr":                   true,
	"net.PacketConn":                 true,
	"net.TCPConn":                    true,
	"net.TCPListener":                true,
	"net.UDPConn":                    true,
	"net/http.ResponseWriter":        true,
	"net/http.Request":               true,
	"mime/multipart.Form":            true,
	"net/http.Server":                true,
	"log.Logger":                     true,
	"net/http.Transport":             true,
	"net/http.ServeMux":              true,
	"net/http.HTTP2Config":           true,
	"net/http.Protocols":             true,
	"crypto/elliptic.Curve":          true,
	"crypto/elliptic.CurveParams":    true,
	"crypto/ecdsa.PrivateKey":        true,
	"crypto/ecdsa.PublicKey":         true,
	"crypto/rsa.PrivateKey":          true,
	"crypto/rsa.PublicKey":           true,
	"crypto/x509.Certificate":        true,
	"crypto/x509.CertificateRequest": true,
	"crypto/x509.CertPool":           true,
	"encoding/pem.Block":             true,
	// These json/xml error types are opaque shims so their methods/fields (.Error(), .Type,
	// .Offset, …) resolve to Json.cs shims — echo's binder calls them. The json/xml decode
	// shims always return a plain GoError, so a real instance is never produced; a type
	// switch `case *json.SyntaxError` therefore must use the PRECISE matcher (IsShimKindStrict,
	// keyed on the registered [GoShim] CLR class) — the loose IsShimKind heuristic falsely
	// matched every GoError. See emitTypeMatch and LIMITATIONS.md.
	"encoding/json.UnmarshalTypeError":   true,
	"encoding/json.SyntaxError":          true,
	"encoding/xml.UnsupportedTypeError":  true,
	"encoding/xml.SyntaxError":           true,
	"crypto/x509/pkix.Name":              true,
	"crypto/x509/pkix.Extension":         true,
	"crypto/tls.Config":                  true,
	"crypto/tls.Certificate":             true,
	"crypto/tls.Conn":                    true,
	"crypto/tls.ConnectionState":         true,
	"crypto/tls.Dialer":                  true,
	"net/http.ResponseController":        true,
	"os/exec.Cmd":                        true,
	"os.Process":                     true,
	"container/list.List":                true,
	"container/list.Element":             true,
	"encoding/csv.Reader":                true,
	"encoding/csv.Writer":                true,
	"compress/gzip.Writer":               true,
	"compress/gzip.Reader":               true,
	"compress/zlib.Writer":               true,
	"compress/flate.Writer":              true,
	"compress/flate.ReadError":           true,
	"compress/flate.WriteError":          true,
	"crypto/cipher.Block":                true,
	"crypto/cipher.AEAD":                 true,
	"crypto/cipher.BlockMode":            true,
	"crypto/cipher.Stream":               true,
	"hash.Hash32":                        true,
	"hash.Hash64":                        true,
	"math/big.Int":                       true,
	"math/big.Float":                     true,
	"math/big.Rat":                       true,
	"hash/maphash.Hash":                  true,
	"hash/maphash.Seed":                  true,
	"encoding/base32.Encoding":           true,
	"strings.Reader":                     true,
	"bytes.Reader":                       true,
	"bufio.Scanner":                      true,
	"bufio.Reader":                       true,
	"bufio.ReadWriter":                   true,
	"mime/multipart.FileHeader":          true,
	"mime/multipart.File":                true,
	"mime/multipart.Reader":              true,
	"mime/multipart.Writer":              true,
	"mime/multipart.Part":                true,
	"net/http.Cookie":                    true,
	"net/http.Client":                    true,
	"net/http/cookiejar.Jar":             true,
	"log/slog.Logger":                    true,
	"log/slog.Attr":                      true,
	"log/slog.Handler":                   true,
	"log/slog.HandlerOptions":            true,
	"log/slog.TextHandler":               true,
	"log/slog.JSONHandler":               true,
	"syscall.Signal":                     true,
	"os.Signal":                          true,
	"net/http/httptest.Server":           true,
	"net/http/httptest.ResponseRecorder": true,
	"bufio.Writer":                       true,
	"time.Ticker":                        true,
	"time.Timer":                         true,
	"encoding/json.Decoder":              true,
	"encoding/json.Encoder":              true,
	"reflect.StructField":                true,
	"reflect.Method":                     true,
	"reflect.MapIter":                    true,
	"runtime.Func":                       true,
}

// shimVarRegistry maps "importpath.VarName" stdlib package variables to a
// no-argument accessor returning the runtime object.
var shimVarRegistry = map[string]shimFunc{
	"os.Stdout":                      {"Os", "Stdout"},
	"os.Stderr":                      {"Os", "Stderr"},
	"os.Stdin":                       {"Os", "Stdin"},
	"os.Args":                        {"Os", "Args"},
	"log/slog.TimeKey":               {"Slog", "KeyTime"},
	"log/slog.MessageKey":            {"Slog", "KeyMessage"},
	"log/slog.LevelKey":              {"Slog", "KeyLevel"},
	"log/slog.SourceKey":             {"Slog", "KeySource"},
	"crypto/rand.Reader":             {"Crypto", "RandReader"},
	"io.Discard":                     {"Io", "Discard"},
	"net/http.DefaultClient":         {"Http", "DefaultClient"},
	"os.Interrupt":                   {"Os", "Interrupt"},
	"os.Kill":                        {"Os", "Kill"},
	"syscall.SIGHUP":                 {"Syscall", "SIGHUP"},
	"syscall.SIGINT":                 {"Syscall", "SIGINT"},
	"syscall.SIGQUIT":                {"Syscall", "SIGQUIT"},
	"syscall.SIGKILL":                {"Syscall", "SIGKILL"},
	"syscall.SIGUSR1":                {"Syscall", "SIGUSR1"},
	"syscall.SIGUSR2":                {"Syscall", "SIGUSR2"},
	"syscall.SIGPIPE":                {"Syscall", "SIGPIPE"},
	"syscall.SIGTERM":                {"Syscall", "SIGTERM"},
	"time.UTC":                       {"Time", "UTC"},
	"time.Local":                     {"Time", "Local"},
	"encoding/base64.StdEncoding":    {"Base64", "StdEncoding"},
	"encoding/base64.URLEncoding":    {"Base64", "URLEncoding"},
	"encoding/base64.RawStdEncoding": {"Base64", "RawStdEncoding"},
	"encoding/base64.RawURLEncoding": {"Base64", "RawURLEncoding"},
	"encoding/binary.LittleEndian":   {"Binary", "LittleEndian"},
	"encoding/binary.BigEndian":      {"Binary", "BigEndian"},
	"hash/crc32.IEEETable":           {"Hashes", "Crc32IEEETable"},
	"bufio.ErrBufferFull":            {"Bufio", "ErrBufferFull"},
	"bufio.ErrNegativeCount":         {"Bufio", "ErrNegativeCount"},
	"path.ErrBadPattern":            {"Path", "ErrBadPattern"},
	"path/filepath.ErrBadPattern":   {"Path", "ErrBadPattern"},
	"encoding/hex.ErrLength":        {"Hex", "ErrLength"},
	"mime.ErrInvalidMediaParameter": {"Mime", "ErrInvalidMediaParameter"},
	"encoding/csv.ErrBareQuote":      {"Csv", "ErrBareQuote"},
	"encoding/csv.ErrQuote":          {"Csv", "ErrQuote"},
	"encoding/csv.ErrFieldCount":     {"Csv", "ErrFieldCount"},
	"encoding/csv.ErrTrailingComma":  {"Csv", "ErrTrailingComma"},
	"net/mail.ErrHeaderNotPresent":   {"Mail", "ErrHeaderNotPresent"},
	"errors.ErrUnsupported":          {"Errors", "ErrUnsupported"},
	"encoding/asn1.NullBytes":        {"Asn1", "NullBytes"},
	"mime/multipart.ErrMessageTooLarge": {"Multipart", "ErrMessageTooLarge"},
	"bufio.ErrInvalidUnreadByte":     {"Bufio", "ErrInvalidUnreadByte"},
	"bufio.ErrInvalidUnreadRune":     {"Bufio", "ErrInvalidUnreadRune"},
	"bufio.ErrTooLong":               {"Bufio", "ErrTooLong"},
	"bufio.ErrNegativeAdvance":       {"Bufio", "ErrNegativeAdvance"},
	"bufio.ErrAdvanceTooFar":         {"Bufio", "ErrAdvanceTooFar"},
	"bufio.ErrBadReadCount":          {"Bufio", "ErrBadReadCount"},
	"bufio.ErrFinalToken":            {"Bufio", "ErrFinalToken"},
	"net.DefaultResolver":            {"Net", "DefaultResolver"},
	"syscall.ForkLock":               {"Syscall", "ForkLock"},
	"compress/gzip.ErrChecksum":      {"Compress", "GzipErrChecksum"},
	"compress/gzip.ErrHeader":        {"Compress", "GzipErrHeader"},
	"compress/zlib.ErrChecksum":      {"Compress", "ZlibErrChecksum"},
	"compress/zlib.ErrHeader":        {"Compress", "ZlibErrHeader"},
	"compress/zlib.ErrDictionary":    {"Compress", "ZlibErrDictionary"},
	"net.IPv4zero":                   {"Net", "IPv4zero"},
	"net.IPv4bcast":                  {"Net", "IPv4bcast"},
	"net.IPv4allsys":                 {"Net", "IPv4allsys"},
	"net.IPv4allrouter":              {"Net", "IPv4allrouter"},
	"net.IPv6zero":                   {"Net", "IPv6zero"},
	"net.IPv6unspecified":            {"Net", "IPv6unspecified"},
	"net.IPv6loopback":               {"Net", "IPv6loopback"},
	"encoding/base32.StdEncoding":    {"Base32", "StdEncoding"},
	"encoding/base32.HexEncoding":    {"Base32", "HexEncoding"},
	"context.Canceled":               {"Context", "Canceled"},
	"context.DeadlineExceeded":       {"Context", "DeadlineExceeded"},
	"io.EOF":                         {"Io", "EOF"},
	"bytes.ErrTooLarge":              {"BytesBuffer", "ErrTooLarge"},
	"encoding/binary.NativeEndian":   {"Binary", "NativeEndian"},
	"encoding/xml.HTMLAutoClose":     {"Xml", "HTMLAutoClose"},
	"encoding/xml.HTMLEntity":        {"Xml", "HTMLEntity"},
	"io.ErrUnexpectedEOF":            {"Io", "ErrUnexpectedEOF"},
	"net.ErrClosed":                  {"Net", "ErrClosed"},
	"net/http.ErrAbortHandler":       {"Http", "ErrAbortHandler"},
	"net/http.ErrBodyNotAllowed":     {"Http", "ErrBodyNotAllowed"},
	"net/http.ErrNotSupported":       {"Http", "ErrNotSupported"},
	"net/http.ErrSkipAltProtocol":    {"Http", "ErrSkipAltProtocol"},
	"net/http.ErrServerClosed":       {"Http", "ErrServerClosed"},
	"net/http.ErrHandlerTimeout":     {"Http", "ErrHandlerTimeout"},
	"net/http.ErrNoCookie":           {"Http", "ErrNoCookie"},
	"net/http.NoBody":                {"Http", "NoBody"},
	"net/http.DefaultServeMux":       {"HttpTypes", "DefaultServeMux"},
	"net/http.LocalAddrContextKey":   {"Http", "LocalAddrContextKey"},
	"net/http.ServerContextKey":      {"Http", "ServerContextKey"},
	"os.ErrDeadlineExceeded":         {"Os", "ErrDeadlineExceeded"},
	"os.ErrNotExist":                 {"Os", "ErrNotExist"},
	"flag.CommandLine":               {"Flag", "CommandLine"},
	"flag.ErrHelp":                   {"Flag", "ErrHelpVar"},
	"os.ErrExist":                    {"Os", "ErrExist"},
	"os.ErrClosed":                   {"Os", "ErrClosed"},
	"os.ErrPermission":               {"Os", "ErrPermission"},
	"os.ErrInvalid":                  {"Os", "ErrInvalid"},
	"os.ErrProcessDone":              {"Os", "ErrProcessDone"},
	"encoding/xml.Header":            {"Xml", "Header"},
	"io/fs.ErrClosed":                {"Os", "ErrClosed"},
	"io/fs.ErrNotExist":              {"Os", "ErrNotExist"},
	"io/fs.ErrExist":                 {"Os", "ErrExist"},
	"io/fs.ErrPermission":            {"Os", "ErrPermission"},
	"io/fs.ErrInvalid":               {"Os", "ErrInvalid"},
	"io/fs.SkipDir":                  {"Fs", "SkipDir"},
	"io/fs.SkipAll":                  {"Fs", "SkipAll"},
	"path/filepath.SkipDir":          {"Fs", "SkipDir"},
	"path/filepath.SkipAll":          {"Fs", "SkipAll"},
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
	// An explicit generic instantiation of a shimmed function used as a value
	// (cmp.Compare[int], cmp.Less[string]) — wrap the shim; the type args don't change
	// the runtime dispatch.
	case *ast.IndexExpr:
		return l.shimFuncValue(x.X)
	case *ast.IndexListExpr:
		return l.shimFuncValue(x.X)
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
	"mime/multipart.Part": {
		"Header": {"Multipart", "Part_Header"},
	},
	"net/http.Cookie": {
		"Name": {"Http", "Cookie_Name"}, "Value": {"Http", "Cookie_Value"}, "Path": {"Http", "Cookie_Path"},
		"Domain": {"Http", "Cookie_Domain"}, "MaxAge": {"Http", "Cookie_MaxAge"}, "Secure": {"Http", "Cookie_Secure"}, "HttpOnly": {"Http", "Cookie_HttpOnly"}, "SameSite": {"Http", "Cookie_SameSite"},
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
		"User": {"Url", "URL_User"}, "RawPath": {"Url", "URL_RawPath"}, "Opaque": {"Url", "URL_Opaque"},
		"RawFragment": {"Url", "URL_RawFragment"}, "ForceQuery": {"Url", "URL_ForceQuery"},
	},
	"net.IPNet": {
		"IP": {"Net", "IPNet_IP"}, "Mask": {"Net", "IPNet_Mask"},
	},
	"net.UDPAddr": {
		"IP": {"Net", "UDPAddr_IP"}, "Port": {"Net", "UDPAddr_Port"}, "Zone": {"Net", "UDPAddr_Zone"},
	},
	"net.TCPAddr": { // GoNetAddr is shared with net.UDPAddr; reuse its field getters.
		"IP": {"Net", "UDPAddr_IP"}, "Port": {"Net", "UDPAddr_Port"}, "Zone": {"Net", "UDPAddr_Zone"},
	},
	"net.IPAddr": {
		"IP": {"Net", "UDPAddr_IP"}, "Zone": {"Net", "UDPAddr_Zone"},
	},
	"net.Interface": {"Index": {"Net", "Interface_Index"}, "Name": {"Net", "Interface_Name"}, "HardwareAddr": {"Net", "Interface_HardwareAddr"}},
	"os/exec.Cmd":  {"Process": {"Exec", "Cmd_Process"}},
	"os.Process":   {"Pid": {"Exec", "Process_Pid"}},
	"syscall.SockaddrInet4": {"Addr": {"Syscall", "Sockaddr_Addr"}},
	"syscall.SockaddrInet6": {"Addr": {"Syscall", "Sockaddr_Addr"}},
	"net/http/httptest.Server": {
		"URL": {"Httptest", "Server_URL"},
	},
	"net/http/httptest.ResponseRecorder": {
		"Code": {"Httptest", "Recorder_Code"}, "Body": {"Httptest", "Recorder_Body"}, "HeaderMap": {"Httptest", "Recorder_HeaderMap"},
	},
	"log/slog.Attr": {
		"Key": {"Slog", "Attr_Key"}, "Value": {"Slog", "Attr_Value"},
	},
	"crypto/x509.Certificate": {
		"Subject": {"Crypto509", "Cert_Subject"}, "DNSNames": {"Crypto509", "Cert_DNSNames"},
		"Raw": {"Crypto509", "Cert_Raw"}, "NotBefore": {"Crypto509", "Cert_NotBefore"}, "NotAfter": {"Crypto509", "Cert_NotAfter"},
		"Version": {"Crypto509", "Cert_Version"}, "Issuer": {"Crypto509", "Cert_Issuer"}, "KeyUsage": {"Crypto509", "Cert_KeyUsage"},
		"ExtKeyUsage": {"Crypto509", "Cert_ExtKeyUsage"}, "ExtraExtensions": {"Crypto509", "Cert_ExtraExtensions"},
		"IPAddresses": {"Crypto509", "Cert_IPAddresses"}, "PublicKey": {"Crypto509", "Cert_PublicKey"},
	},
	"encoding/pem.Block": {
		"Type": {"Pem", "Block_Type"}, "Bytes": {"Pem", "Block_Bytes"}, "Headers": {"Pem", "Block_Headers"},
	},
	"encoding/json.UnmarshalTypeError": {
		"Type": {"Json", "UTE_Type"}, "Value": {"Json", "UTE_Value"}, "Field": {"Json", "UTE_Field"}, "Struct": {"Json", "UTE_Struct"}, "Offset": {"Json", "UTE_Offset"},
	},
	"encoding/json.SyntaxError": {
		"Offset": {"Json", "SyntaxErr_Offset"},
	},
	"encoding/xml.UnsupportedTypeError": {
		"Type": {"Json", "UTE_Type"},
	},
	"encoding/xml.SyntaxError": {
		"Line": {"Json", "SyntaxErr_Offset"}, "Msg": {"Json", "UTE_Struct"},
	},
	"crypto/x509/pkix.Name": {
		"CommonName": {"Crypto509", "PkixName_CommonName"}, "Organization": {"Crypto509", "PkixName_Organization"},
	},
	"crypto/rsa.PublicKey": {
		"N": {"Crypto509", "RsaKey_N"}, "E": {"Crypto509", "RsaKey_E"},
	},
	"crypto/ecdsa.PublicKey": {
		"X": {"Crypto509", "EcKey_X"}, "Y": {"Crypto509", "EcKey_Y"}, "Curve": {"Crypto509", "EcKey_Curve"},
	},
	"crypto/elliptic.CurveParams": {
		"Name": {"Crypto509", "CurveParams_Name"}, "BitSize": {"Crypto509", "CurveParams_BitSize"},
		"P": {"Crypto509", "CurveParams_P"}, "N": {"Crypto509", "CurveParams_N"}, "B": {"Crypto509", "CurveParams_B"},
		"Gx": {"Crypto509", "CurveParams_Gx"}, "Gy": {"Crypto509", "CurveParams_Gy"},
	},
	"crypto/ecdsa.PrivateKey": {
		"PublicKey": {"Crypto509", "EcdsaPublic"}, "X": {"Crypto509", "EcKey_X"}, "Y": {"Crypto509", "EcKey_Y"}, "Curve": {"Crypto509", "EcKey_Curve"},
	},
	"crypto/rsa.PrivateKey": {
		"PublicKey": {"Crypto509", "RsaPublic"}, "N": {"Crypto509", "RsaKey_N"}, "E": {"Crypto509", "RsaKey_E"},
	},
	"sync.Cond": {
		"L": {"Sync", "Cond_L"},
	},
	"net/mail.Address": {
		"Name": {"Mail", "Address_Name"}, "Address": {"Mail", "Address_Address"},
	},
	"go/token.Position": {"Filename": {"GoToken", "Position_Filename"}, "Offset": {"GoToken", "Position_Offset"}, "Line": {"GoToken", "Position_Line"}, "Column": {"GoToken", "Position_Column"}},
	"encoding/csv.ParseError": {"StartLine": {"Csv", "ParseError_StartLine"}, "Line": {"Csv", "ParseError_Line"}, "Column": {"Csv", "ParseError_Column"}, "Err": {"Csv", "ParseError_Err"}},
	"time.ParseError":          {"Layout": {"Time", "PErr_Layout"}, "Value": {"Time", "PErr_Value"}, "LayoutElem": {"Time", "PErr_LayoutElem"}, "ValueElem": {"Time", "PErr_ValueElem"}, "Message": {"Time", "PErr_Message"}},
	"net/textproto.Error": {"Code": {"Textproto", "Error_Code"}, "Msg": {"Textproto", "Error_Msg"}},
	"flag.Flag": {"Name": {"Flag", "Flag_Name"}, "Usage": {"Flag", "Flag_Usage"}, "DefValue": {"Flag", "Flag_DefValue"}, "Value": {"Flag", "Flag_Value"}},
	"compress/gzip.Writer": {"Name": {"Compress", "Writer_Name"}, "Comment": {"Compress", "Writer_Comment"}},
	"compress/gzip.Reader": {"Name": {"Compress", "GzReader_Name"}, "Comment": {"Compress", "GzReader_Comment"}},
	"net/mail.Message": {"Header": {"Mail", "Message_Header"}, "Body": {"Mail", "Message_Body"}},
	"io/fs.PathError": {"Op": {"Fs", "PathError_Op"}, "Path": {"Fs", "PathError_Path"}, "Err": {"Fs", "PathError_Err"}},
	"os.PathError": {"Op": {"Fs", "PathError_Op"}, "Path": {"Fs", "PathError_Path"}, "Err": {"Fs", "PathError_Err"}},
	"os.LinkError": {"Op": {"Os", "LinkError_Op"}, "Old": {"Os", "LinkError_Old"}, "New": {"Os", "LinkError_New"}, "Err": {"Os", "LinkError_Err"}},
	"text/template.ExecError": {"Name": {"Template", "ExecError_Name"}, "Err": {"Template", "ExecError_Err"}},
	"html/template.Error": {"ErrorCode": {"Template", "HtmlError_ErrorCode"}, "Name": {"Template", "HtmlError_Name"}, "Line": {"Template", "HtmlError_Line"}, "Description": {"Template", "HtmlError_Description"}},
	"net/http.ProtocolError": {"ErrorString": {"Http", "ProtocolError_ErrorString"}},
	"net/http.MaxBytesError": {"Limit": {"Http", "MaxBytesError_Limit"}},
	"compress/flate.ReadError": {"Offset": {"Compress", "ReadError_Offset"}, "Err": {"Compress", "ReadError_Err"}},
	"compress/flate.WriteError": {"Offset": {"Compress", "WriteError_Offset"}, "Err": {"Compress", "WriteError_Err"}},
	"text/scanner.Position": {"Filename": {"GoToken", "Position_Filename"}, "Offset": {"GoToken", "Position_Offset"}, "Line": {"GoToken", "Position_Line"}, "Column": {"GoToken", "Position_Column"}},
	"text/scanner.Scanner": {"Position": {"Scanner", "Scanner_Position"}, "Filename": {"Scanner", "Scanner_Filename"}, "Line": {"Scanner", "Scanner_PLine"}, "Column": {"Scanner", "Scanner_PColumn"}, "Offset": {"Scanner", "Scanner_POffset"}, "Mode": {"Scanner", "Scanner_Mode"}, "ErrorCount": {"Scanner", "Scanner_ErrorCount"}},
	"encoding/asn1.StructuralError": {"Msg": {"Asn1", "StructuralError_Msg"}},
	"encoding/asn1.SyntaxError":     {"Msg": {"Asn1", "SyntaxError_Msg"}},
	"encoding/asn1.BitString":       {"Bytes": {"Asn1", "BitString_GetBytes"}, "BitLength": {"Asn1", "BitString_GetBitLength"}},
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
	"crypto/tls.Certificate": {
		"PrivateKey": {"HttpTypes", "Cert_PrivateKey"}, "Leaf": {"HttpTypes", "Cert_Leaf"}, "Certificate": {"HttpTypes", "Cert_Certificate"}, "OCSPStaple": {"HttpTypes", "Cert_OCSPStaple"},
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
	"reflect.SliceHeader": {
		"Data": {"Reflect", "SH_Data"}, "Len": {"Reflect", "SH_Len"}, "Cap": {"Reflect", "SH_Cap"},
	},
	"reflect.StringHeader": {
		"Data": {"Reflect", "SH_Data"}, "Len": {"Reflect", "SH_Len"},
	},
	"runtime.Frame": {
		"File": {"Goruntime", "Frame_File"}, "Function": {"Goruntime", "Frame_Function"},
		"Line": {"Goruntime", "Frame_Line"}, "PC": {"Goruntime", "Frame_PC"}, "Entry": {"Goruntime", "Frame_Entry"},
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
	"net.Dialer": {"LocalAddr": {"Net", "Dialer_SetLocalAddr"}},
	"os/exec.Cmd": {"Stdout": {"Exec", "Cmd_SetStdout"}, "Stderr": {"Exec", "Cmd_SetStderr"}, "Env": {"Exec", "Cmd_SetEnv"}},
	"syscall.SockaddrInet4": {"Port": {"Syscall", "Sockaddr_SetPort"}},
	"syscall.SockaddrInet6": {"Port": {"Syscall", "Sockaddr_SetPort"}, "ZoneId": {"Syscall", "Sockaddr_SetZoneId"}},
	"encoding/xml.Name": {
		"Space": {"Xml", "Name_SetSpace"}, "Local": {"Xml", "Name_SetLocal"},
	},
	"net.UDPAddr": {
		"IP": {"Net", "UDPAddr_SetIP"}, "Port": {"Net", "UDPAddr_SetPort"},
	},
	"net.IPNet": {
		"IP": {"Net", "IPNet_SetIP"}, "Mask": {"Net", "IPNet_SetMask"},
	},
	"log/slog.HandlerOptions": {
		"Level": {"Slog", "HO_SetLevel"}, "AddSource": {"Slog", "HO_SetAddSource"}, "ReplaceAttr": {"Slog", "HO_SetReplaceAttr"},
	},
	"crypto/x509.Certificate": {
		"SerialNumber": {"Crypto509", "Cert_SetSerialNumber"}, "Subject": {"Crypto509", "Cert_SetSubject"},
		"NotBefore": {"Crypto509", "Cert_SetNotBefore"}, "NotAfter": {"Crypto509", "Cert_SetNotAfter"},
		"DNSNames": {"Crypto509", "Cert_SetDNSNames"}, "KeyUsage": {"Crypto509", "Cert_SetKeyUsage"},
		"ExtKeyUsage": {"Crypto509", "Cert_SetExtKeyUsage"}, "IsCA": {"Crypto509", "Cert_SetIsCA"},
		"BasicConstraintsValid": {"Crypto509", "Cert_SetBasicConstraintsValid"},
		"ExtraExtensions":       {"Crypto509", "Cert_SetExtraExtensions"}, "IPAddresses": {"Crypto509", "Cert_SetIPAddresses"},
		"PublicKey": {"Crypto509", "Cert_SetPublicKey"},
	},
	"encoding/pem.Block": {
		"Type": {"Pem", "Block_SetType"}, "Bytes": {"Pem", "Block_SetBytes"}, "Headers": {"Pem", "Block_SetHeaders"},
	},
	"crypto/x509.CertificateRequest": {
		"Subject": {"Crypto509", "CertReq_SetSubject"}, "DNSNames": {"Crypto509", "CertReq_SetDNSNames"},
	},
	"crypto/x509/pkix.Name": {
		"CommonName": {"Crypto509", "PkixName_SetCommonName"}, "Organization": {"Crypto509", "PkixName_SetOrganization"}, "Country": {"Crypto509", "PkixName_SetCountry"},
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
	"net/http.Client": {
		"Timeout": {"Http", "Client_SetTimeout"}, "Transport": {"Http", "Client_SetTransport"}, "CheckRedirect": {"Http", "Client_SetCheckRedirect"}, "Jar": {"Http", "Client_SetJar"},
	},
	"net/http.Cookie": {
		"Name": {"Http", "Cookie_SetName"}, "Value": {"Http", "Cookie_SetValue"}, "Path": {"Http", "Cookie_SetPath"},
		"Domain": {"Http", "Cookie_SetDomain"}, "MaxAge": {"Http", "Cookie_SetMaxAge"}, "Secure": {"Http", "Cookie_SetSecure"}, "HttpOnly": {"Http", "Cookie_SetHttpOnly"}, "SameSite": {"Http", "Cookie_SetSameSite"},
	},
	"net/mail.Address": {
		"Name": {"Mail", "Address_SetName"}, "Address": {"Mail", "Address_SetAddress"},
	},
	"go/token.Position": {"Filename": {"GoToken", "Position_SetFilename"}, "Offset": {"GoToken", "Position_SetOffset"}, "Line": {"GoToken", "Position_SetLine"}, "Column": {"GoToken", "Position_SetColumn"}},
	"encoding/csv.ParseError": {"StartLine": {"Csv", "ParseError_SetStartLine"}, "Line": {"Csv", "ParseError_SetLine"}, "Column": {"Csv", "ParseError_SetColumn"}, "Err": {"Csv", "ParseError_SetErr"}},
	"encoding/csv.Reader": {"Comma": {"Csv", "Reader_SetComma"}, "Comment": {"Csv", "Reader_SetComment"}, "LazyQuotes": {"Csv", "Reader_SetLazyQuotes"}, "TrimLeadingSpace": {"Csv", "Reader_SetTrimLeadingSpace"}, "FieldsPerRecord": {"Csv", "Reader_SetFieldsPerRecord"}, "ReuseRecord": {"Csv", "Reader_SetReuseRecord"}},
	"encoding/csv.Writer": {"Comma": {"Csv", "Writer_SetComma"}, "UseCRLF": {"Csv", "Writer_SetUseCRLF"}},
	"compress/gzip.Writer": {"Name": {"Compress", "Writer_SetName"}, "Comment": {"Compress", "Writer_SetComment"}},
	"net/textproto.Error": {"Code": {"Textproto", "Error_SetCode"}, "Msg": {"Textproto", "Error_SetMsg"}},
	"io/fs.PathError": {"Op": {"Fs", "PathError_SetOp"}, "Path": {"Fs", "PathError_SetPath"}, "Err": {"Fs", "PathError_SetErr"}},
	"os.PathError": {"Op": {"Fs", "PathError_SetOp"}, "Path": {"Fs", "PathError_SetPath"}, "Err": {"Fs", "PathError_SetErr"}},
	"os.LinkError": {"Op": {"Os", "LinkError_SetOp"}, "Old": {"Os", "LinkError_SetOld"}, "New": {"Os", "LinkError_SetNew"}, "Err": {"Os", "LinkError_SetErr"}},
	"text/template.ExecError": {"Name": {"Template", "ExecError_SetName"}, "Err": {"Template", "ExecError_SetErr"}},
	"html/template.Error": {"ErrorCode": {"Template", "HtmlError_SetErrorCode"}, "Name": {"Template", "HtmlError_SetName"}, "Line": {"Template", "HtmlError_SetLine"}, "Description": {"Template", "HtmlError_SetDescription"}},
	"net/http.ProtocolError": {"ErrorString": {"Http", "ProtocolError_SetErrorString"}},
	"net/http.MaxBytesError": {"Limit": {"Http", "MaxBytesError_SetLimit"}},
	"compress/flate.ReadError": {"Offset": {"Compress", "ReadError_SetOffset"}, "Err": {"Compress", "ReadError_SetErr"}},
	"compress/flate.WriteError": {"Offset": {"Compress", "WriteError_SetOffset"}, "Err": {"Compress", "WriteError_SetErr"}},
	"text/scanner.Position": {"Filename": {"GoToken", "Position_SetFilename"}, "Offset": {"GoToken", "Position_SetOffset"}, "Line": {"GoToken", "Position_SetLine"}, "Column": {"GoToken", "Position_SetColumn"}},
	"text/scanner.Scanner": {"Filename": {"Scanner", "Scanner_SetFilename"}, "Mode": {"Scanner", "Scanner_SetMode"}},
	"encoding/asn1.StructuralError": {"Msg": {"Asn1", "StructuralError_SetMsg"}},
	"encoding/asn1.SyntaxError":     {"Msg": {"Asn1", "SyntaxError_SetMsg"}},
	"encoding/asn1.BitString":       {"Bytes": {"Asn1", "BitString_SetBytes"}, "BitLength": {"Asn1", "BitString_SetBitLength"}},
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
		"ErrorLog": {"HttpTypes", "Server_SetErrorLog"}, "ReadTimeout": {"HttpTypes", "Server_SetReadTimeout"}, "WriteTimeout": {"HttpTypes", "Server_SetWriteTimeout"}, "ReadHeaderTimeout": {"HttpTypes", "Server_SetReadHeaderTimeout"}, "MaxHeaderBytes": {"HttpTypes", "Server_SetMaxHeaderBytes"},
	},
	"net/http.Transport": {
		"TLSNextProto": {"HttpTypes", "Transport_SetTLSNextProto"}, "TLSClientConfig": {"HttpTypes", "Transport_SetTLSClientConfig"}, "HTTP2": {"HttpTypes", "Transport_SetHTTP2"},
	},
	"crypto/tls.Config": {
		"NextProtos": {"HttpTypes", "Config_SetNextProtos"}, "PreferServerCipherSuites": {"HttpTypes", "Config_SetPreferServerCipherSuites"},
		"ServerName": {"HttpTypes", "Config_SetServerName"}, "MinVersion": {"HttpTypes", "Config_SetMinVersion"}, "MaxVersion": {"HttpTypes", "Config_SetMaxVersion"}, "InsecureSkipVerify": {"HttpTypes", "Config_SetInsecureSkipVerify"},
		"Certificates": {"HttpTypes", "Config_SetCertificates"}, "GetCertificate": {"HttpTypes", "Config_SetGetCertificate"},
	},
	"crypto/tls.Certificate": {
		"PrivateKey": {"HttpTypes", "Cert_SetPrivateKey"}, "Leaf": {"HttpTypes", "Cert_SetLeaf"}, "Certificate": {"HttpTypes", "Cert_SetCertificate"}, "OCSPStaple": {"HttpTypes", "Cert_SetOCSPStaple"},
	},
	"net/url.URL": {
		"Path": {"Url", "URL_SetPath"}, "Scheme": {"Url", "URL_SetScheme"},
		"Host": {"Url", "URL_SetHost"}, "RawQuery": {"Url", "URL_SetRawQuery"},
		"Fragment": {"Url", "URL_SetFragment"}, "User": {"Url", "URL_SetUser"},
		"Opaque": {"Url", "URL_SetOpaque"},
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
	// A shim var is named either qualified from another package (io.EOF -> SelectorExpr)
	// or bare from within its own compiled-from-source package (io's multiReader.Read
	// referencing EOF -> Ident); both must resolve to the shim accessor, not the package's
	// compiled global, so the single sentinel is shared (err == io.EOF holds everywhere).
	var idn *ast.Ident
	switch x := e.(type) {
	case *ast.SelectorExpr:
		idn = x.Sel
	case *ast.Ident:
		idn = x
	default:
		return nil, false
	}
	// Accept a package var OR a typed const (e.g. syscall.SIGINT): both name a shim
	// value the accessor must produce rather than fold to a constant.
	obj := l.pkg.TypesInfo.Uses[idn]
	if obj == nil || obj.Pkg() == nil {
		return nil, false
	}
	switch obj.(type) {
	case *types.Var, *types.Const:
	default:
		return nil, false
	}
	sf, ok := shimVarRegistry[obj.Pkg().Path()+"."+obj.Name()]
	if !ok {
		return nil, false
	}
	return &goir.Extern{Assembly: shimAssembly, Namespace: shimAssembly, Type: sf.csType, Method: sf.csMethod, Ret: goir.TObject}, true
}

// opaqueZeroCtor maps an opaque value-type shim to the constructor producing its
// (non-null) zero value; types absent here zero to null (e.g. reflect handles).
var opaqueZeroCtor = map[string]shimFunc{
	"sync.Mutex":                     {"Sync", "NewMutex"},
	"sync.RWMutex":                   {"Sync", "NewRWMutex"},
	"sync.WaitGroup":                 {"Sync", "NewWaitGroup"},
	"sync.Once":                      {"Sync", "NewOnce"},
	"sync.Map":                       {"Sync", "NewMap"},
	"sync.Pool":                      {"Sync", "NewPool"},
	"sync.Cond":                      {"Sync", "NewCondZero"},
	"runtime.Frame":                  {"Goruntime", "FrameZero"},
	"sync/atomic.Value":              {"Atomic", "NewValue"},
	"syscall.SockaddrInet4":          {"Syscall", "NewSockaddrInet4"},
	"syscall.SockaddrInet6":          {"Syscall", "NewSockaddrInet6"},
	"net/http.Server":                {"HttpTypes", "NewServer"},
	"net/http.Cookie":                {"Http", "NewCookie"},
	"net/mail.Address":               {"Mail", "NewAddress"},
	"net/url.URL":                    {"Url", "URL_Zero"},
	"go/token.Position":               {"GoToken", "PositionZero"},
	"encoding/csv.ParseError":          {"Csv", "ParseErrorZero"},
	"net/textproto.Error":              {"Textproto", "Error_Zero"},
	"io/fs.PathError":                  {"Fs", "PathErrorZero"},
	"os.PathError":                     {"Fs", "PathErrorZero"},
	"os.LinkError":                     {"Os", "LinkErrorZero"},
	"text/template.ExecError":          {"Template", "ExecErrorZero"},
	"net/http.ProtocolError":            {"Http", "ProtocolErrorZero"},
	"net/http.MaxBytesError":            {"Http", "MaxBytesErrorZero"},
	"html/template.Error":              {"Template", "HtmlErrorZero"},
	"compress/flate.ReadError":         {"Compress", "ReadErrorZero"},
	"compress/flate.WriteError":        {"Compress", "WriteErrorZero"},
	"text/scanner.Position":            {"GoToken", "ScannerPositionZero"},
	"text/scanner.Scanner":             {"Scanner", "NewScannerZero"},
	"go/token.FileSet":                {"GoToken", "FileSetZero"},
	"encoding/asn1.StructuralError":  {"Asn1", "NewStructuralError"},
	"encoding/asn1.SyntaxError":      {"Asn1", "NewSyntaxError"},
	"encoding/asn1.BitString":       {"Asn1", "NewBitString"},
	"net/http.Client":                {"Http", "NewClient"},
	"net/netip.Addr":                 {"Netip", "AddrZero"},
	"net/netip.AddrPort":             {"Netip", "AddrPortZero"},
	"net/netip.Prefix":               {"Netip", "PrefixZero"},
	"flag.FlagSet":                   {"Flag", "NewFlagSetZero"},
	"mime.WordDecoder":               {"Mime", "WordDecoderZero"},
	"index/suffixarray.Index":         {"Suffixarray", "NewZero"},
	"log.Logger":                     {"Log", "NewLoggerZero"},
	"net/http.Transport":             {"HttpTypes", "NewTransport"},
	"crypto/tls.Config":              {"HttpTypes", "NewTlsConfig"},
	"crypto/tls.Certificate":         {"HttpTypes", "NewTlsCert"},
	"crypto/tls.Conn":                {"HttpTypes", "NewTlsConn"},
	"crypto/tls.ConnectionState":     {"HttpTypes", "NewTlsConnState"},
	"net/http.HTTP2Config":           {"HttpTypes", "NewHTTP2Config"},
	"net/http.Protocols":             {"HttpTypes", "NewProtocols"},
	"sync/atomic.Bool":               {"Atomic", "NewBool"},
	"sync/atomic.Int64":              {"AtomicInt", "NewInt"},
	"sync/atomic.Int32":              {"AtomicInt", "NewInt"},
	"sync/atomic.Uint64":             {"AtomicInt", "NewUint"},
	"sync/atomic.Uint32":             {"AtomicInt", "NewUint"},
	"sync/atomic.Uintptr":            {"AtomicInt", "NewUint"},
	"sync/atomic.Pointer":            {"AtomicInt", "NewPointer"},
	"strings.Builder":                {"StringsBuilder", "New"},
	"bytes.Buffer":                   {"BytesBuffer", "New"},
	"time.Time":                      {"Time", "TimeZero"},
	"math/big.Int":                   {"Big", "IntZero"},
	"math/big.Float":                 {"Big", "FloatZero"},
	"math/big.Rat":                   {"Big", "RatZero"},
	"hash/maphash.Hash":              {"MapHash", "New"},
	"net.IPNet":                      {"Net", "NewIPNet"},
	"net.UDPAddr":                    {"Net", "NewUDPAddr"},
	"log/slog.Attr":                  {"Slog", "NewAttr"},
	"log/slog.HandlerOptions":        {"Slog", "NewHandlerOptions"},
	"encoding/pem.Block":             {"Pem", "NewBlock"},
	"crypto/x509/pkix.Name":          {"Crypto509", "NewPkixName"},
	"crypto/x509/pkix.Extension":     {"Crypto509", "NewPkixExt"},
	"crypto/x509.Certificate":        {"Crypto509", "NewCertificate"},
	"crypto/x509.CertificateRequest": {"Crypto509", "NewCertReq"},
	"syscall.Flock_t":                {"Syscall", "NewFlockT"},
	"encoding/xml.Name":              {"Xml", "NewXmlName"},
	"encoding/xml.StartElement":      {"Xml", "NewXmlStart"},
	"encoding/xml.EndElement":        {"Xml", "NewXmlEnd"},
	"encoding/xml.Attr":              {"Xml", "NewXmlAttr"},
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
	"AppendUint16": {"Binary", "AppendUint16"}, "AppendUint32": {"Binary", "AppendUint32"}, "AppendUint64": {"Binary", "AppendUint64"},
}

var shimMethodRegistry = map[string]map[string]shimFunc{
	"reflect.Method": {"IsExported": {"Reflect", "Method_IsExported"}},
	"reflect.MapIter": {
		"Next": {"Reflect", "MapIter_Next"}, "Key": {"Reflect", "MapIter_Key"},
		"Value": {"Reflect", "MapIter_Value"}, "Reset": {"Reflect", "MapIter_Reset"},
	},
	"runtime.Frames": {"Next": {"Goruntime", "Frames_Next"}},
	"mime.WordEncoder": {"Encode": {"Mime", "WordEncoder_Encode"}},
	"mime.WordDecoder": {"Decode": {"Mime", "WordDecoder_Decode"}, "DecodeHeader": {"Mime", "WordDecoder_DecodeHeader"}},
	"index/suffixarray.Index": {"Bytes": {"Suffixarray", "Index_Bytes"}, "FindAllIndex": {"Suffixarray", "Index_FindAllIndex"}, "Lookup": {"Suffixarray", "Lookup"}},
	"mime/quotedprintable.Writer": {"Write": {"QuotedPrintable", "QPWriter_Write"}, "Close": {"QuotedPrintable", "QPWriter_Close"}},
	"mime/quotedprintable.Reader": {"Read": {"QuotedPrintable", "QPReader_Read"}},
	"go/token.Token": {"String": {"GoToken", "Token_String"}, "IsLiteral": {"GoToken", "Token_IsLiteral"}, "IsOperator": {"GoToken", "Token_IsOperator"}, "IsKeyword": {"GoToken", "Token_IsKeyword"}, "Precedence": {"GoToken", "Token_Precedence"}},
	"go/token.Position": {"String": {"GoToken", "Position_String"}, "IsValid": {"GoToken", "Position_IsValid"}},
	"go/token.Pos": {"IsValid": {"GoToken", "Pos_IsValid"}},
	"encoding/csv.ParseError": {"Error": {"Csv", "ParseError_Error"}, "Unwrap": {"Csv", "ParseError_Unwrap"}},
	"time.ParseError":          {"Error": {"Time", "PErr_Error"}},
	"text/scanner.Position": {"String": {"GoToken", "ScannerPosition_String"}, "IsValid": {"GoToken", "Position_IsValid"}},
	"text/scanner.Scanner": {"Init": {"Scanner", "Scanner_Init"}, "Scan": {"Scanner", "Scanner_Scan"}, "Next": {"Scanner", "Scanner_Next"}, "Peek": {"Scanner", "Scanner_Peek"}, "Pos": {"Scanner", "Scanner_Pos"}, "TokenText": {"Scanner", "Scanner_TokenText"}},
	"go/token.FileSet": {"Base": {"GoToken", "FileSet_Base"}, "AddFile": {"GoToken", "FileSet_AddFile"}, "File": {"GoToken", "FileSet_File"}, "Position": {"GoToken", "FileSet_Position"}, "PositionFor": {"GoToken", "FileSet_PositionFor"}, "Iterate": {"GoToken", "FileSet_Iterate"}, "RemoveFile": {"GoToken", "FileSet_RemoveFile"}},
	"go/token.File": {"Name": {"GoToken", "File_Name"}, "Base": {"GoToken", "File_Base"}, "Size": {"GoToken", "File_Size"}, "LineCount": {"GoToken", "File_LineCount"}, "AddLine": {"GoToken", "File_AddLine"}, "Offset": {"GoToken", "File_Offset"}, "Pos": {"GoToken", "File_Pos"}, "Line": {"GoToken", "File_Line"}, "Position": {"GoToken", "File_Position"}, "PositionFor": {"GoToken", "File_PositionFor"}, "LineStart": {"GoToken", "File_LineStart"}, "SetLines": {"GoToken", "File_SetLines"}, "End": {"GoToken", "File_End"}, "Lines": {"GoToken", "File_Lines"}, "MergeLine": {"GoToken", "File_MergeLine"}, "SetLinesForContent": {"GoToken", "File_SetLinesForContent"}, "AddLineInfo": {"GoToken", "File_AddLineInfo"}, "AddLineColumnInfo": {"GoToken", "File_AddLineColumnInfo"}},
	"strconv.NumError": {
		"Error": {"Strconv", "NumError_Error"}, "Unwrap": {"Strconv", "NumError_Unwrap"},
	},
	"encoding/json.Number": {
		"Float64": {"Json", "Number_Float64"}, "Int64": {"Json", "Number_Int64"}, "String": {"Json", "Number_String"},
	},
	"encoding/json.Delim":      {"String": {"Json", "Delim_String"}},
	"encoding/json.RawMessage": {"MarshalJSON": {"Json", "RawMessage_MarshalJSON"}, "UnmarshalJSON": {"Json", "RawMessage_UnmarshalJSON"}},
	"encoding/json.UnsupportedTypeError":  {"Error": {"Json", "UnsupportedTypeError_Error"}},
	"encoding/json.UnsupportedValueError": {"Error": {"Json", "UnsupportedValueError_Error"}},
	"encoding/json.InvalidUTF8Error":      {"Error": {"Json", "InvalidUTF8Error_Error"}},
	"encoding/json.InvalidUnmarshalError": {"Error": {"Json", "InvalidUnmarshalError_Error"}},
	"encoding/json.UnmarshalFieldError":   {"Error": {"Json", "UnmarshalFieldError_Error"}},
	"encoding/json.MarshalerError":        {"Error": {"Json", "MarshalerError_Error"}, "Unwrap": {"Json", "MarshalerError_Unwrap"}},
	"encoding/json.UnmarshalTypeError": {
		"Error": {"Json", "UTE_Error"},
	},
	"encoding/json.SyntaxError": {
		"Error": {"Json", "SyntaxErr_Error"},
	},
	"encoding/xml.UnsupportedTypeError": {
		"Error": {"Json", "UTE_Error"},
	},
	"encoding/xml.SyntaxError": {
		"Error": {"Json", "SyntaxErr_Error"},
	},
	"net/http.ServeMux": {
		"Handle": {"HttpTypes", "Mux_Handle"}, "HandleFunc": {"HttpTypes", "Mux_HandleFunc"}, "ServeHTTP": {"HttpTypes", "Mux_ServeHTTP"}, "Handler": {"HttpTypes", "Mux_Handler"},
	},
	"mime/multipart.Form": {
		"RemoveAll": {"Multipart", "Form_RemoveAll"},
	},
	"mime/multipart.Reader": {
		"ReadForm": {"Multipart", "Reader_ReadForm"}, "NextPart": {"Multipart", "Reader_NextPart"}, "NextRawPart": {"Multipart", "Reader_NextRawPart"},
	},
	"mime/multipart.Part": {
		"Read": {"Multipart", "Part_Read"}, "Close": {"Multipart", "Part_Close"}, "FormName": {"Multipart", "Part_FormName"}, "FileName": {"Multipart", "Part_FileName"},
	},
	"mime/multipart.Writer": {
		"SetBoundary": {"Multipart", "Writer_SetBoundary"}, "WriteField": {"Multipart", "Writer_WriteField"},
		"CreatePart": {"Multipart", "Writer_CreatePart"}, "Close": {"Multipart", "Writer_Close"},
		"Boundary": {"Multipart", "Writer_Boundary"}, "FormDataContentType": {"Multipart", "Writer_FormDataContentType"},
		"CreateFormField": {"Multipart", "Writer_CreateFormField"}, "CreateFormFile": {"Multipart", "Writer_CreateFormFile"},
	},
	"crypto/tls.Config": {
		"Clone": {"HttpTypes", "Config_Clone"}, "BuildNameToCertificate": {"HttpTypes", "Config_BuildNameToCertificate"},
	},
	"net.Dialer": { // client dialer — dead code on goclr's server path; dial returns an error.
		"DialContext": {"HttpTypes", "Dialer_DialContext"}, "Dial": {"HttpTypes", "Dialer_Dial"},
	},
	"net.Resolver": { // DNS — dead code on goclr's server path; lookups return an error.
		"LookupIPAddr": {"Net", "Resolver_LookupIPAddr"},
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
		"Fatal": {"Log", "Logger_Fatal"}, "Fatalf": {"Log", "Logger_Fatalf"}, "Fatalln": {"Log", "Logger_Fatalln"}, "Panic": {"Log", "Logger_Panic"}, "Panicf": {"Log", "Logger_Panicf"}, "Panicln": {"Log", "Logger_Panicln"},
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
		"Clone": {"Template", "Tmpl_Clone"},
	},
	"text/template.Template": {
		"New": {"Template", "Tmpl_New"}, "Delims": {"Template", "Tmpl_Delims"}, "Funcs": {"Template", "Tmpl_Funcs"},
		"Parse": {"Template", "Tmpl_Parse"}, "ParseFiles": {"Template", "Tmpl_ParseFiles"}, "ParseGlob": {"Template", "Tmpl_ParseGlob"},
		"Execute": {"Template", "Tmpl_Execute"}, "ExecuteTemplate": {"Template", "Tmpl_ExecuteTemplate"},
		"Templates": {"Template", "Tmpl_Templates"}, "Name": {"Template", "Tmpl_Name"}, "Lookup": {"Template", "Tmpl_Lookup"}, "Option": {"Template", "Tmpl_Option"},
		"Clone": {"Template", "Tmpl_Clone"},
	},
	"encoding/json.Decoder": {
		"Token": {"Json", "Decoder_Token"}, "More": {"Json", "Decoder_More"},
		"Decode": {"Json", "Decoder_Decode"}, "UseNumber": {"Json", "Decoder_UseNumber"}, "DisallowUnknownFields": {"Json", "Decoder_DisallowUnknownFields"},
		"InputOffset": {"Json", "Decoder_InputOffset"},
		"Buffered": {"Json", "Decoder_Buffered"},
	},
	"encoding/json.Encoder": {
		"Encode": {"Json", "Encoder_Encode"}, "SetIndent": {"Json", "Encoder_SetIndent"},
		"SetEscapeHTML": {"Json", "Encoder_SetEscapeHTML"},
	},
	"net/url.URL": {
		"IsAbs": {"Url", "URL_IsAbs"}, "String": {"Url", "URL_String"},
		"ResolveReference": {"Url", "URL_ResolveReference"}, "Query": {"Url", "URL_Query"}, "RequestURI": {"Url", "URL_RequestURI"},
		"Hostname": {"Url", "URL_Hostname"}, "Port": {"Url", "URL_Port"}, "EscapedPath": {"Url", "URL_EscapedPath"}, "EscapedFragment": {"Url", "URL_EscapedFragment"},
		"Redacted": {"Url", "URL_Redacted"}, "Parse": {"Url", "URL_Parse"}, "JoinPath": {"Url", "URL_JoinPath"},
		"MarshalBinary": {"Url", "URL_MarshalBinary"}, "UnmarshalBinary": {"Url", "URL_UnmarshalBinary"}, "AppendBinary": {"Url", "URL_AppendBinary"},
	},
	"net/url.Userinfo": {
		"Username": {"Url", "Userinfo_Username"}, "Password": {"Url", "Userinfo_Password"}, "String": {"Url", "Userinfo_String"},
	},
	"net/url.Error": {
		"Error": {"Url", "URLError_Error"}, "Unwrap": {"Url", "URLError_Unwrap"}, "Timeout": {"Url", "URLError_Timeout"}, "Temporary": {"Url", "URLError_Temporary"},
	},
	"net/url.EscapeError":      {"Error": {"Url", "EscapeError_Error"}},
	"net/url.InvalidHostError": {"Error": {"Url", "InvalidHostError_Error"}},
	"net/url.Values": {
		"Get": {"Url", "Values_Get"}, "Set": {"Url", "Values_Set"}, "Add": {"Url", "Values_Add"},
		"Del": {"Url", "Values_Del"}, "Has": {"Url", "Values_Has"}, "Encode": {"Url", "Values_Encode"},
	},
	"strings.Reader": {
		"Read": {"Readers", "Reader_Read"}, "ReadByte": {"Readers", "Reader_ReadByte"}, "UnreadByte": {"Readers", "Reader_UnreadByte"},
		"ReadRune": {"Readers", "Reader_ReadRune"}, "Len": {"Readers", "Reader_Len"}, "Size": {"Readers", "Reader_Size"},
		"Reset": {"Readers", "Reader_Reset"}, "Seek": {"Readers", "Reader_Seek"}, "ReadAt": {"Readers", "Reader_ReadAt"},
		"WriteTo": {"Readers", "Reader_WriteTo"}, "UnreadRune": {"Readers", "Reader_UnreadRune"},
	},
	"bytes.Reader": {
		"Read": {"Readers", "Reader_Read"}, "ReadByte": {"Readers", "Reader_ReadByte"}, "UnreadByte": {"Readers", "Reader_UnreadByte"},
		"ReadRune": {"Readers", "Reader_ReadRune"}, "Len": {"Readers", "Reader_Len"}, "Size": {"Readers", "Reader_Size"},
		"Reset": {"Readers", "Reader_ResetBytes"}, "Seek": {"Readers", "Reader_Seek"}, "ReadAt": {"Readers", "Reader_ReadAt"},
		"WriteTo": {"Readers", "Reader_WriteTo"}, "UnreadRune": {"Readers", "Reader_UnreadRune"},
	},
	"reflect.Type": {
		"Kind": {"Reflect", "Type_Kind"}, "Name": {"Reflect", "Type_Name"},
		"String": {"Reflect", "Type_String"}, "NumField": {"Reflect", "Type_NumField"},
		"Elem": {"Reflect", "Type_Elem"}, "Key": {"Reflect", "Type_Key"}, "Len": {"Reflect", "Type_Len"},
		"Field": {"Reflect", "Type_Field"}, "FieldByName": {"Reflect", "Type_FieldByName"}, "NumMethod": {"Reflect", "Type_NumMethod"},
		"NumIn": {"Reflect", "Type_NumIn"}, "NumOut": {"Reflect", "Type_NumOut"},
		"In": {"Reflect", "Type_In"}, "Out": {"Reflect", "Type_Out"},
		"AssignableTo": {"Reflect", "Type_AssignableTo"}, "ConvertibleTo": {"Reflect", "Type_ConvertibleTo"},
		"Comparable": {"Reflect", "Type_Comparable"}, "Implements": {"Reflect", "Type_Implements"},
		"PkgPath": {"Reflect", "Type_PkgPath"}, "Method": {"Reflect", "Type_Method"}, "MethodByName": {"Reflect", "Type_MethodByName"},
	},
	"reflect.Kind": {
		"String": {"Reflect", "Kind_String"},
	},
	"reflect.StructTag": {
		"Get": {"Reflect", "StructTag_Get"}, "Lookup": {"Reflect", "StructTag_Lookup"},
	},
	"reflect.StructField": {
		"IsExported": {"Reflect", "StructField_IsExported"},
	},
	"runtime.Func": {
		"Name": {"Goruntime", "Func_Name"}, "FileLine": {"Goruntime", "Func_FileLine"},
		"Entry": {"Goruntime", "Func_Entry"},
	},
	"bufio.Scanner": {
		"Scan": {"Bufio", "Scanner_Scan"}, "Text": {"Bufio", "Scanner_Text"}, "Bytes": {"Bufio", "Scanner_Bytes"}, "Err": {"Bufio", "Scanner_Err"},
		"Split": {"Bufio", "Scanner_Split"}, "Buffer": {"Bufio", "Scanner_Buffer"},
	},
	"bufio.Reader": {
		"Read": {"Bufio", "Reader_Read"}, "ReadByte": {"Bufio", "Reader_ReadByte"}, "UnreadByte": {"Bufio", "Reader_UnreadByte"}, "Peek": {"Bufio", "Reader_Peek"}, "Discard": {"Bufio", "Reader_Discard"}, "Reset": {"Bufio", "Reader_Reset"}, "Buffered": {"Bufio", "Reader_Buffered"},
		"ReadString": {"Bufio", "Reader_ReadString"}, "ReadBytes": {"Bufio", "Reader_ReadBytes"},
		"Size": {"Bufio", "Reader_Size"}, "ReadRune": {"Bufio", "Reader_ReadRune"}, "UnreadRune": {"Bufio", "Reader_UnreadRune"},
		"ReadSlice": {"Bufio", "Reader_ReadSlice"}, "ReadLine": {"Bufio", "Reader_ReadLine"}, "WriteTo": {"Bufio", "Reader_WriteTo"},
	},
	"bufio.ReadWriter": {
		"Flush": {"Bufio", "RW_Flush"}, "Write": {"Bufio", "Writer_Write"}, "Read": {"Bufio", "RW_Read"},
	},
	"bufio.Writer": {
		"Available": {"Bufio", "Writer_Available"}, "Buffered": {"Bufio", "Writer_Buffered"}, "Flush": {"Bufio", "Writer_Flush"},
		"Write": {"Bufio", "Writer_Write"}, "WriteByte": {"Bufio", "Writer_WriteByte"}, "WriteString": {"Bufio", "Writer_WriteString"}, "Reset": {"Bufio", "Writer_Reset"},
		"Size": {"Bufio", "Writer_Size"}, "WriteRune": {"Bufio", "Writer_WriteRune"}, "AvailableBuffer": {"Bufio", "Writer_AvailableBuffer"}, "ReadFrom": {"Bufio", "Writer_ReadFrom"},
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
	"net/http/cookiejar.Jar": {
		"SetCookies": {"Cookiejar", "Jar_SetCookies"}, "Cookies": {"Cookiejar", "Jar_Cookies"},
	},
	"net/http.Client": {
		"Do": {"Http", "Client_Do"}, "Get": {"Http", "Client_Get"}, "Post": {"Http", "Client_Post"}, "Head": {"Http", "Client_Head"},
	},
	"log/slog.Logger": {
		"Info": {"Slog", "Logger_Info"}, "Debug": {"Slog", "Logger_Debug"}, "Warn": {"Slog", "Logger_Warn"},
		"Error": {"Slog", "Logger_Error"}, "With": {"Slog", "Logger_With"},
		"WithGroup": {"Slog", "Logger_WithGroup"},
	},
	"syscall.Signal": {
		"String": {"Ossignal", "Signal_String"}, "Signal": {"Ossignal", "Signal_Signal"},
	},
	"crypto.Hash": {
		"Available": {"Crypto", "CHash_Available"}, "Size": {"Crypto", "CHash_Size"}, "New": {"Crypto", "CHash_New"}, "HashFunc": {"Crypto", "CHash_HashFunc"},
	},
	"crypto/x509.Certificate": {
		"VerifyHostname": {"Crypto509", "Cert_VerifyHostname"}, "CheckSignatureFrom": {"Crypto509", "Cert_CheckSignatureFrom"},
	},
	"crypto/x509.CertPool": {
		"AppendCertsFromPEM": {"Crypto509", "CertPool_AppendCertsFromPEM"},
	},
	"crypto/ecdsa.PrivateKey":  {"Public": {"Crypto509", "EcdsaPublic"}},
	"crypto/rsa.PrivateKey":    {"Public": {"Crypto509", "RsaPublic"}},
	"crypto/elliptic.Curve": {
		"Params": {"Crypto509", "Curve_Params"}, "IsOnCurve": {"Crypto509", "Curve_IsOnCurve"},
		"Add": {"Crypto509", "Curve_Add"}, "Double": {"Crypto509", "Curve_Double"},
		"ScalarMult": {"Crypto509", "Curve_ScalarMult"}, "ScalarBaseMult": {"Crypto509", "Curve_ScalarBaseMult"},
	},
	"net/http/httptest.Server": {
		"Close": {"Httptest", "Server_Close"}, "Client": {"Httptest", "Server_Client"}, "Start": {"Httptest", "Server_Start"},
	},
	"net/http/httptest.ResponseRecorder": {
		"Header": {"Httptest", "Recorder_Header"}, "Write": {"Httptest", "Recorder_Write"},
		"WriteHeader": {"Httptest", "Recorder_WriteHeader"},
	},
	"encoding/xml.Encoder": {
		"Encode": {"Xml", "Encoder_Encode"}, "EncodeElement": {"Xml", "Encoder_EncodeElement"}, "EncodeToken": {"Xml", "Encoder_EncodeToken"},
		"Flush": {"Xml", "Encoder_Flush"}, "Indent": {"Xml", "Encoder_Indent"}, "Close": {"Xml", "Encoder_Close"},
	},
	"encoding/xml.Decoder": {
		"Decode": {"Xml", "Decoder_Decode"}, "Token": {"Xml", "Decoder_Token"},
	},
	"encoding/xml.StartElement": {"Copy": {"Xml", "StartElement_Copy"}, "End": {"Xml", "StartElement_End"}},
	"encoding/xml.CharData":     {"Copy": {"Xml", "CharData_Copy"}},
	"encoding/xml.Comment":      {"Copy": {"Xml", "Comment_Copy"}},
	"encoding/xml.Directive":    {"Copy": {"Xml", "Directive_Copy"}},
	"encoding/xml.ProcInst":     {"Copy": {"Xml", "ProcInst_Copy"}},
	"encoding/xml.TagPathError":  {"Error": {"Xml", "TagPathError_Error"}},
	"encoding/xml.UnmarshalError": {"Error": {"Xml", "UnmarshalError_Error"}},
	"io.ReadCloser": {
		"Close": {"Http", "Body_Close"},
	},
	"net/http.Request": {
		"ParseForm": {"Http", "Req_ParseForm"}, "ParseMultipartForm": {"Http", "Req_ParseMultipartForm"}, "Context": {"Http", "Req_Context"},
		"WithContext": {"Http", "Req_WithContext"}, "Clone": {"Http", "Req_Clone"}, "UserAgent": {"Http", "Req_UserAgent"}, "Referer": {"Http", "Req_Referer"}, "Cookie": {"Http", "Req_Cookie"}, "Cookies": {"Http", "Req_Cookies"}, "AddCookie": {"Http", "Req_AddCookie"}, "FormFile": {"Http", "Req_FormFile"}, "MultipartReader": {"Http", "Req_MultipartReader"},
		"FormValue": {"Http", "Req_FormValue"}, "PostFormValue": {"Http", "Req_PostFormValue"},
		"PathValue": {"Http", "Req_PathValue"},
	},
	"net/http.ResponseWriter": {
		"Write": {"Http", "RW_Write"}, "WriteHeader": {"Http", "RW_WriteHeader"}, "Header": {"Http", "RW_Header"},
	},
	// net.Listener is an interface, not a concrete shim handle: keep it OUT of this
	// registry so a method call on a net.Listener value goes through interface dispatch
	// (matching net.TCPListener for a plain GoListener, or navigating the embedded shim
	// for a wrapper like echo's tcpKeepAliveListener) rather than being short-circuited
	// here with the raw interface value passed as the receiver.
	"net.TCPListener": {
		"Accept": {"Net", "Listener_Accept"}, "AcceptTCP": {"Net", "Listener_AcceptTCP"}, "Close": {"Net", "Listener_Close"}, "Addr": {"Net", "Listener_Addr"},
	},
	"net.Conn": {
		"Read": {"Net", "Conn_Read"}, "Write": {"Net", "Conn_Write"}, "Close": {"Net", "Conn_Close"},
	},
	"net.TCPConn": {
		"SetKeepAlive": {"Net", "TCPConn_SetKeepAlive"}, "SetKeepAlivePeriod": {"Net", "TCPConn_SetKeepAlivePeriod"}, "SetNoDelay": {"Net", "TCPConn_SetNoDelay"}, "SetLinger": {"Net", "TCPConn_SetLinger"},
		"Read": {"Net", "Conn_Read"}, "Write": {"Net", "Conn_Write"}, "Close": {"Net", "Conn_Close"},
		"SetReadDeadline": {"Net", "Conn_SetReadDeadline"}, "SetWriteDeadline": {"Net", "Conn_SetWriteDeadline"}, "SetDeadline": {"Net", "Conn_SetDeadline"},
		"LocalAddr": {"Net", "Conn_LocalAddr"}, "RemoteAddr": {"Net", "Conn_RemoteAddr"},
	},
	"net.UDPConn": {
		"ReadFromUDP": {"Net", "UDPConn_ReadFromUDP"}, "WriteToUDP": {"Net", "UDPConn_WriteToUDP"},
		"Read": {"Net", "UDPConn_Read"}, "Write": {"Net", "UDPConn_Write"}, "Close": {"Net", "UDPConn_Close"},
		"LocalAddr":       {"Net", "UDPConn_LocalAddr"},
		"SetReadDeadline": {"Net", "UDPConn_SetReadDeadline"}, "SetWriteDeadline": {"Net", "UDPConn_SetWriteDeadline"}, "SetDeadline": {"Net", "UDPConn_SetDeadline"},
	},
	"net.UDPAddr": {
		"String": {"Net", "UDPAddr_String"}, "Network": {"Net", "UDPAddr_Network"},
	},
	"net.TCPAddr": { // shares GoNetAddr with net.UDPAddr; reuse its String/Network.
		"String": {"Net", "UDPAddr_String"}, "Network": {"Net", "TCPAddr_Network"},
	},
	"os/exec.Cmd": {
		"Output": {"Exec", "Cmd_Output"}, "CombinedOutput": {"Exec", "Cmd_CombinedOutput"}, "Run": {"Exec", "Cmd_Run"},
		"Start": {"Exec", "Cmd_Start"}, "Wait": {"Exec", "Cmd_Wait"},
	},
	"os.Process": {
		"Kill": {"Exec", "Process_Kill"}, "Wait": {"Exec", "Process_Wait"},
	},
	"container/list.List": {
		"Len": {"List", "List_Len"}, "Front": {"List", "List_Front"}, "Back": {"List", "List_Back"},
		"PushBack": {"List", "List_PushBack"}, "PushFront": {"List", "List_PushFront"}, "Remove": {"List", "List_Remove"},
		"MoveToFront": {"List", "List_MoveToFront"}, "MoveToBack": {"List", "List_MoveToBack"},
		"Init": {"List", "List_Init"}, "InsertBefore": {"List", "List_InsertBefore"}, "InsertAfter": {"List", "List_InsertAfter"},
		"MoveBefore": {"List", "List_MoveBefore"}, "MoveAfter": {"List", "List_MoveAfter"},
		"PushBackList": {"List", "List_PushBackList"}, "PushFrontList": {"List", "List_PushFrontList"},
	},
	"container/list.Element": {
		"Next": {"List", "Element_Next"}, "Prev": {"List", "Element_Prev"},
	},
	"encoding/csv.Reader": {
		"ReadAll": {"Csv", "ReadAll"}, "Read": {"Csv", "Read"}, "FieldPos": {"Csv", "FieldPos"}, "InputOffset": {"Csv", "InputOffset"},
	},
	"encoding/csv.Writer": {
		"Write": {"Csv", "Write"}, "Flush": {"Csv", "Flush"}, "WriteAll": {"Csv", "WriteAll"}, "Error": {"Csv", "Error"},
	},
	"encoding/hex.encoder": {
		"Write": {"Hex", "Encoder_Write"},
	},
	"encoding/hex.dumper": {
		"Write": {"Hex", "Dumper_Write"}, "Close": {"Hex", "Dumper_Close"},
	},
	"encoding/base64.encoder": {
		"Write": {"Base64", "Encoder_Write"}, "Close": {"Base64", "Encoder_Close"},
	},
	"compress/gzip.Reader": {
		"Read": {"Compress", "CompR_Read"}, "Reset": {"Compress", "CompR_Reset"}, "Close": {"Compress", "CompR_Close"}, "Multistream": {"Compress", "CompR_Multistream"},
	},
	"compress/gzip.Writer": {
		"Write": {"Compress", "CompW_Write"}, "Close": {"Compress", "CompW_Close"}, "Flush": {"Compress", "CompW_Flush"}, "Reset": {"Compress", "CompW_Reset"},
	},
	"compress/zlib.Writer": {
		"Write": {"Compress", "CompW_Write"}, "Close": {"Compress", "CompW_Close"}, "Flush": {"Compress", "CompW_Flush"}, "Reset": {"Compress", "CompW_Reset"},
	},
	"compress/flate.Writer": {
		"Write": {"Compress", "CompW_Write"}, "Close": {"Compress", "CompW_Close"}, "Flush": {"Compress", "CompW_Flush"}, "Reset": {"Compress", "CompW_Reset"},
	},
	"compress/flate.CorruptInputError": {"Error": {"Compress", "CorruptInputError_Error"}},
	"compress/flate.InternalError":     {"Error": {"Compress", "InternalError_Error"}},
	"compress/flate.ReadError":         {"Error": {"Compress", "ReadError_Error"}},
	"compress/flate.WriteError":        {"Error": {"Compress", "WriteError_Error"}},
	"crypto/cipher.AEAD": {
		"Seal": {"Aes", "GCM_Seal"}, "Open": {"Aes", "GCM_Open"}, "NonceSize": {"Aes", "GCM_NonceSize"}, "Overhead": {"Aes", "GCM_Overhead"},
	},
	"crypto/cipher.BlockMode": {
		"CryptBlocks": {"Aes", "CBC_CryptBlocks"}, "BlockSize": {"Aes", "CBC_BlockSize"},
	},
	"crypto/cipher.Stream": {
		"XORKeyStream": {"Aes", "Stream_XORKeyStream"}, // dispatches to CTR/CFB/OFB by handle
	},
	"crypto/cipher.Block": {
		"Encrypt": {"Aes", "Block_Encrypt"}, "Decrypt": {"Aes", "Block_Decrypt"}, "BlockSize": {"Aes", "Block_BlockSize"},
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
		"Strict": {"Base64", "Strict"},
		"WithPadding": {"Base64", "WithPadding"}, "AppendEncode": {"Base64", "AppendEncode"}, "AppendDecode": {"Base64", "AppendDecode"},
	},
	"encoding/base64.CorruptInputError": {"Error": {"Base64", "CorruptInputError_Error"}},
	"encoding/binary.littleEndian": binaryMethods,
	"encoding/binary.bigEndian":    binaryMethods,
	"encoding/binary.ByteOrder":    binaryMethods,
	"hash.Hash": {
		"Write": {"Crypto", "Hash_Write"}, "Sum": {"Crypto", "Hash_Sum"}, "Reset": {"Crypto", "Hash_Reset"},
		"Size": {"Crypto", "Hash_Size"}, "BlockSize": {"Crypto", "Hash_BlockSize"},
	},
	// fmt.State: handed to a user Format(fmt.State, rune); its methods dispatch to the shim
	// GoFmtState the formatter builds.
	"fmt.State": {
		"Write": {"Fmt", "State_Write"}, "Width": {"Fmt", "State_Width"},
		"Precision": {"Fmt", "State_Precision"}, "Flag": {"Fmt", "State_Flag"},
	},
	"encoding/hex.InvalidByteError": {
		"Error": {"Hex", "InvalidByteError_Error"},
	},
	// Go 1.24's crypto/sha3.New* return the concrete *sha3.SHA3 (not hash.Hash). It is an
	// opaque shim handle (the GoHash the constructor builds); its hash.Hash methods reuse
	// the shared Hash_* shims, which operate on that GoHash.
	"crypto/sha3.SHA3": {
		"Write": {"Crypto", "Hash_Write"}, "Sum": {"Crypto", "Hash_Sum"}, "Reset": {"Crypto", "Hash_Reset"},
		"Size": {"Crypto", "Hash_Size"}, "BlockSize": {"Crypto", "Hash_BlockSize"},
	},
	"regexp.Regexp": {
		"MatchString": {"Regexp", "Re_MatchString"}, "Match": {"Regexp", "Re_Match"}, "FindString": {"Regexp", "Re_FindString"},
		"FindStringSubmatch": {"Regexp", "Re_FindStringSubmatch"}, "FindAllString": {"Regexp", "Re_FindAllString"},
		"FindAllStringSubmatch": {"Regexp", "Re_FindAllStringSubmatch"},
		"ReplaceAllString":      {"Regexp", "Re_ReplaceAllString"}, "Split": {"Regexp", "Re_Split"},
		"ReplaceAllStringFunc":     {"Regexp", "Re_ReplaceAllStringFunc"},
		"ReplaceAllLiteralString":  {"Regexp", "Re_ReplaceAllLiteralString"},
		"String": {"Regexp", "Re_String"}, "FindStringIndex": {"Regexp", "Re_FindStringIndex"}, "FindAllStringSubmatchIndex": {"Regexp", "Re_FindAllStringSubmatchIndex"},
		"SubexpNames": {"Regexp", "Re_SubexpNames"}, "NumSubexp": {"Regexp", "Re_NumSubexp"},
		"FindStringSubmatchIndex": {"Regexp", "Re_FindStringSubmatchIndex"},
		"FindReaderSubmatchIndex": {"Regexp", "Re_FindReaderSubmatchIndex"},
		"Find": {"Regexp", "Re_Find"}, "FindIndex": {"Regexp", "Re_FindIndex"}, "FindAll": {"Regexp", "Re_FindAll"}, "FindAllIndex": {"Regexp", "Re_FindAllIndex"},
		"FindAllStringIndex": {"Regexp", "Re_FindStringIndexAll"},
		"FindSubmatch": {"Regexp", "Re_FindSubmatch"}, "FindSubmatchIndex": {"Regexp", "Re_FindSubmatchIndex"},
		"FindAllSubmatch": {"Regexp", "Re_FindAllSubmatch"}, "FindAllSubmatchIndex": {"Regexp", "Re_FindAllSubmatchIndex"},
		"ReplaceAll": {"Regexp", "Re_ReplaceAll"}, "ReplaceAllLiteral": {"Regexp", "Re_ReplaceAllLiteral"}, "ReplaceAllFunc": {"Regexp", "Re_ReplaceAllFunc"},
		"SubexpIndex": {"Regexp", "Re_SubexpIndex"}, "LiteralPrefix": {"Regexp", "Re_LiteralPrefix"}, "Longest": {"Regexp", "Re_Longest"}, "Copy": {"Regexp", "Re_Copy"},
		"MarshalText": {"Regexp", "Re_MarshalText"}, "AppendText": {"Regexp", "Re_AppendText"}, "UnmarshalText": {"Regexp", "Re_UnmarshalText"},
		"Expand": {"Regexp", "Re_Expand"}, "ExpandString": {"Regexp", "Re_ExpandString"},
		"MatchReader": {"Regexp", "Re_MatchReader"}, "FindReaderIndex": {"Regexp", "Re_FindReaderIndex"},
	},
	"encoding/base32.Encoding": {
		"EncodeToString": {"Base32", "EncodeToString"}, "DecodeString": {"Base32", "DecodeString"},
		"Encode": {"Base32", "Enc_Encode"}, "Decode": {"Base32", "Enc_Decode"}, "EncodedLen": {"Base32", "Enc_EncodedLen"}, "DecodedLen": {"Base32", "Enc_DecodedLen"},
		"AppendEncode": {"Base32", "Enc_AppendEncode"}, "AppendDecode": {"Base32", "Enc_AppendDecode"}, "WithPadding": {"Base32", "Enc_WithPadding"},
	},
	"encoding/base32.CorruptInputError": {"Error": {"Base32", "CorruptInputError_Error"}},
	"math/big.Int": {
		"Add": {"Big", "Int_Add"}, "Sub": {"Big", "Int_Sub"}, "Mul": {"Big", "Int_Mul"},
		"Div": {"Big", "Int_Div"}, "Mod": {"Big", "Int_Mod"}, "Neg": {"Big", "Int_Neg"},
		"Quo": {"Big", "Int_Quo"}, "Rem": {"Big", "Int_Rem"}, "GCD": {"Big", "Int_GCD"},
		"Abs": {"Big", "Int_Abs"}, "Exp": {"Big", "Int_Exp"}, "Set": {"Big", "Int_Set"}, "FillBytes": {"Big", "Int_FillBytes"},
		"ModSqrt": {"Big", "Int_ModSqrt"}, "Rand": {"Big", "Int_Rand"},
		"Cmp": {"Big", "Int_Cmp"}, "Sign": {"Big", "Int_Sign"}, "Int64": {"Big", "Int_Int64"},
		"String": {"Big", "Int_String"}, "SetString": {"Big", "Int_SetString"},
		"SetInt64": {"Big", "Int_SetInt64"}, "SetUint64": {"Big", "Int_SetUint64"},
		"Lsh": {"Big", "Int_Lsh"}, "Rsh": {"Big", "Int_Rsh"}, "SetBytes": {"Big", "Int_SetBytes"},
		"Bits": {"Big", "Int_Bits"}, "SetBits": {"Big", "Int_SetBits"},
		"Bytes": {"Big", "Int_Bytes"}, "Text": {"Big", "Int_Text"}, "DivMod": {"Big", "Int_DivMod"},
		"Uint64": {"Big", "Int_Uint64"}, "And": {"Big", "Int_And"}, "Or": {"Big", "Int_Or"},
		"Xor": {"Big", "Int_Xor"}, "Not": {"Big", "Int_Not"}, "BitLen": {"Big", "Int_BitLen"},
		"IsInt64": {"Big", "Int_IsInt64"}, "IsUint64": {"Big", "Int_IsUint64"},
		"CmpAbs": {"Big", "Int_CmpAbs"}, "Sqrt": {"Big", "Int_Sqrt"}, "ProbablyPrime": {"Big", "Int_ProbablyPrime"},
		"QuoRem": {"Big", "Int_QuoRem"},
		"Bit": {"Big", "Int_Bit"}, "SetBit": {"Big", "Int_SetBit"}, "TrailingZeroBits": {"Big", "Int_TrailingZeroBits"}, "AndNot": {"Big", "Int_AndNot"},
		"MulRange": {"Big", "Int_MulRange"}, "Binomial": {"Big", "Int_Binomial"}, "Float64": {"Big", "Int_Float64"}, "ModInverse": {"Big", "Int_ModInverse"},
		"MarshalText": {"Big", "Int_MarshalText"}, "UnmarshalText": {"Big", "Int_UnmarshalText"}, "MarshalJSON": {"Big", "Int_MarshalJSON"}, "UnmarshalJSON": {"Big", "Int_UnmarshalJSON"},
		"Append": {"Big", "Int_Append"}, "AppendText": {"Big", "Int_AppendText"}, "GobEncode": {"Big", "Int_GobEncode"}, "GobDecode": {"Big", "Int_GobDecode"},
	},
	"math/big.Float": {
		"SetInt": {"Big", "Float_SetInt"}, "Sub": {"Big", "Float_Sub"}, "Cmp": {"Big", "Float_Cmp"},
		"Sign": {"Big", "Float_Sign"}, "IsInt": {"Big", "Float_IsInt"}, "String": {"Big", "Float_String"},
		"Text": {"Big", "Float_Text"}, "Int": {"Big", "Float_Int"}, "SetString": {"Big", "Float_SetString"},
		"Add": {"Big", "Float_Add"}, "Mul": {"Big", "Float_Mul"}, "Quo": {"Big", "Float_Quo"},
		"Neg": {"Big", "Float_Neg"}, "Abs": {"Big", "Float_Abs"}, "Set": {"Big", "Float_Set"}, "Copy": {"Big", "Float_Copy"},
		"SetFloat64": {"Big", "Float_SetFloat64"}, "SetInt64": {"Big", "Float_SetInt64"}, "SetUint64": {"Big", "Float_SetUint64"},
		"Float64": {"Big", "Float_Float64"}, "IsInf": {"Big", "Float_IsInf"},
		"SetPrec": {"Big", "Float_SetPrec"}, "SetMode": {"Big", "Float_SetMode"}, "Prec": {"Big", "Float_Prec"},
		"MinPrec": {"Big", "Float_MinPrec"}, "Mode": {"Big", "Float_Mode"}, "Acc": {"Big", "Float_Acc"},
	},
	"math/big.Rat": {
		"Add": {"Big", "Rat_Add"}, "Sub": {"Big", "Rat_Sub"}, "Mul": {"Big", "Rat_Mul"}, "Quo": {"Big", "Rat_Quo"},
		"Neg": {"Big", "Rat_Neg"}, "Inv": {"Big", "Rat_Inv"}, "Abs": {"Big", "Rat_Abs"}, "Set": {"Big", "Rat_Set"},
		"String": {"Big", "Rat_String"}, "RatString": {"Big", "Rat_RatString"}, "Num": {"Big", "Rat_Num"}, "Denom": {"Big", "Rat_Denom"},
		"Sign": {"Big", "Rat_Sign"}, "IsInt": {"Big", "Rat_IsInt"}, "Cmp": {"Big", "Rat_Cmp"},
		"SetFrac64": {"Big", "Rat_SetFrac64"}, "SetInt64": {"Big", "Rat_SetInt64"}, "SetInt": {"Big", "Rat_SetInt"},
		"SetFrac": {"Big", "Rat_SetFrac"}, "SetString": {"Big", "Rat_SetString"},
		"FloatString": {"Big", "Rat_FloatString"}, "Float64": {"Big", "Rat_Float64"}, "SetFloat64": {"Big", "Rat_SetFloat64"}, "SetUint64": {"Big", "Rat_SetUint64"},
		"MarshalText": {"Big", "Rat_MarshalText"}, "AppendText": {"Big", "Rat_AppendText"}, "UnmarshalText": {"Big", "Rat_UnmarshalText"},
		"GobEncode": {"Big", "Rat_GobEncode"}, "GobDecode": {"Big", "Rat_GobDecode"},
	},
	"hash/maphash.Hash": {
		"WriteByte": {"MapHash", "WriteByte"}, "Write": {"MapHash", "Write"}, "WriteString": {"MapHash", "WriteString"},
		"Sum64": {"MapHash", "Sum64"}, "Reset": {"MapHash", "Reset"}, "Size": {"MapHash", "Size"}, "BlockSize": {"MapHash", "BlockSize"},
		"SetSeed": {"MapHash", "SetSeed"}, "Seed": {"MapHash", "Seed"},
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
		"Write":     {"BytesBuffer", "Write"}, "Read": {"BytesBuffer", "Read"}, "String": {"BytesBuffer", "String"},
		"Bytes": {"BytesBuffer", "Bytes"}, "Len": {"BytesBuffer", "Len"}, "Reset": {"BytesBuffer", "Reset"},
		"Truncate": {"BytesBuffer", "Truncate"}, "Grow": {"BytesBuffer", "Grow"},
		"ReadByte": {"BytesBuffer", "ReadByte"}, "ReadRune": {"BytesBuffer", "ReadRune"}, "Next": {"BytesBuffer", "Next"},
		"WriteTo": {"BytesBuffer", "WriteTo"},
		"Cap": {"BytesBuffer", "Cap"}, "Available": {"BytesBuffer", "Available"}, "AvailableBuffer": {"BytesBuffer", "AvailableBuffer"},
		"Peek": {"BytesBuffer", "Peek"}, "ReadBytes": {"BytesBuffer", "ReadBytes"}, "ReadFrom": {"BytesBuffer", "ReadFrom"},
		"UnreadByte": {"BytesBuffer", "UnreadByte"}, "UnreadRune": {"BytesBuffer", "UnreadRune"},
	},
	"os.File": {
		"Fd": {"Os", "File_Fd"}, "Close": {"Os", "File_Close"}, "Write": {"Os", "File_Write"}, "WriteString": {"Os", "File_WriteString"}, "Read": {"Os", "File_Read"}, "Name": {"Os", "File_Name"}, "Sync": {"Os", "File_Sync"}, "WriteAt": {"Os", "File_WriteAt"}, "ReadAt": {"Os", "File_ReadAt"}, "Seek": {"Os", "File_Seek"}, "Truncate": {"Os", "File_Truncate"}, "Stat": {"Os", "File_Stat"},
	},
	"crypto/ed25519.PrivateKey": {
		"Public": {"CryptoSign", "Ed25519PrivateKey_Public"}, "Seed": {"CryptoSign", "Ed25519PrivateKey_Seed"}, "Sign": {"CryptoSign", "Ed25519PrivateKey_Sign"},
	},
	"net.IP": {
		"To4": {"Net", "IP_To4"}, "To16": {"Net", "IP_To16"}, "Equal": {"Net", "IP_Equal"}, "String": {"Net", "IP_String"},
		"IsLoopback": {"Net", "IP_IsLoopback"}, "IsLinkLocalUnicast": {"Net", "IP_IsLinkLocalUnicast"}, "IsLinkLocalMulticast": {"Net", "IP_IsLinkLocalMulticast"},
		"IsMulticast": {"Net", "IP_IsMulticast"}, "IsPrivate": {"Net", "IP_IsPrivate"}, "IsUnspecified": {"Net", "IP_IsUnspecified"}, "IsGlobalUnicast": {"Net", "IP_IsGlobalUnicast"},
		"Mask": {"Net", "IP_Mask"}, "DefaultMask": {"Net", "IP_DefaultMask"}, "IsInterfaceLocalMulticast": {"Net", "IP_IsInterfaceLocalMulticast"},
		"MarshalText": {"Net", "IP_MarshalText"}, "AppendText": {"Net", "IP_AppendText"},
	},
	"net.IPMask": {
		"Size": {"Net", "IPMask_Size"}, "String": {"Net", "IPMask_String"},
	},
	"net/textproto.MIMEHeader": {
		"Get": {"Textproto", "MIMEHeader_Get"}, "Set": {"Textproto", "MIMEHeader_Set"}, "Add": {"Textproto", "MIMEHeader_Add"},
		"Del": {"Textproto", "MIMEHeader_Del"}, "Values": {"Textproto", "MIMEHeader_Values"},
	},
	"net/textproto.Reader": {
		"ReadLine": {"Textproto", "Reader_ReadLine"}, "ReadLineBytes": {"Textproto", "Reader_ReadLineBytes"},
		"ReadContinuedLine": {"Textproto", "Reader_ReadContinuedLine"}, "ReadContinuedLineBytes": {"Textproto", "Reader_ReadContinuedLineBytes"},
		"ReadMIMEHeader": {"Textproto", "Reader_ReadMIMEHeader"}, "ReadCodeLine": {"Textproto", "Reader_ReadCodeLine"},
		"ReadResponse": {"Textproto", "Reader_ReadResponse"}, "ReadDotBytes": {"Textproto", "Reader_ReadDotBytes"},
		"ReadDotLines": {"Textproto", "Reader_ReadDotLines"}, "DotReader": {"Textproto", "DotReader"},
	},
	"net/textproto.Writer": {
		"PrintfLine": {"Textproto", "Writer_PrintfLine"}, "DotWriter": {"Textproto", "Writer_DotWriter"},
	},
	"net/textproto.Error": {
		"Error": {"Textproto", "Error_Error"},
	},
	"net/textproto.ProtocolError": {
		"Error": {"Textproto", "ProtocolError_Error"},
	},
	"net/textproto.dotWriter": {
		"Write": {"Textproto", "DotWriter_Write"}, "Close": {"Textproto", "DotWriter_Close"},
	},
	"net/mail.Address": {
		"String": {"Mail", "Address_String"},
	},
	"encoding/asn1.ObjectIdentifier": {
		"String": {"Asn1", "OID_String"}, "Equal": {"Asn1", "OID_Equal"},
	},
	"encoding/asn1.StructuralError": {
		"Error": {"Asn1", "StructuralError_Error"},
	},
	"encoding/asn1.SyntaxError": {
		"Error": {"Asn1", "SyntaxError_Error"},
	},
	"encoding/asn1.BitString": {
		"At": {"Asn1", "BitString_At"}, "RightAlign": {"Asn1", "BitString_RightAlign"},
	},
	"net/mail.AddressParser": {
		"Parse": {"Mail", "AddressParser_Parse"}, "ParseList": {"Mail", "AddressParser_ParseList"},
	},
	"net/mail.Header": {
		"Get": {"Mail", "Header_Get"}, "AddressList": {"Mail", "Header_AddressList"},
	},
	"io/fs.PathError": {
		"Error": {"Fs", "PathError_Error"}, "Unwrap": {"Fs", "PathError_Unwrap"}, "Timeout": {"Fs", "PathError_Timeout"},
	},
	"os.PathError": {
		"Error": {"Fs", "PathError_Error"}, "Unwrap": {"Fs", "PathError_Unwrap"}, "Timeout": {"Fs", "PathError_Timeout"},
	},
	"os.LinkError": {
		"Error": {"Os", "LinkError_Error"}, "Unwrap": {"Os", "LinkError_Unwrap"},
	},
	"text/template.ExecError": {
		"Error": {"Template", "ExecError_Error"}, "Unwrap": {"Template", "ExecError_Unwrap"},
	},
	"html/template.Error": {
		"Error": {"Template", "HtmlError_Error"},
	},
	"net/http.ProtocolError": {"Error": {"Http", "ProtocolError_Error"}, "Is": {"Http", "ProtocolError_Is"}},
	"net/http.MaxBytesError": {"Error": {"Http", "MaxBytesError_Error"}},
	"crypto/x509.PublicKeyAlgorithm": {
		"String": {"Crypto509", "PublicKeyAlgorithm_String"},
	},
	"crypto/x509.SignatureAlgorithm": {
		"String": {"Crypto509", "SignatureAlgorithm_String"},
	},
	"crypto/x509.KeyUsage": {
		"String": {"Crypto509", "KeyUsage_String"},
	},
	"crypto/x509.ExtKeyUsage": {
		"String": {"Crypto509", "ExtKeyUsage_String"},
	},
	"crypto/tls.ClientAuthType": {
		"String": {"HttpTypes", "ClientAuthType_String"},
	},
	"crypto/tls.CurveID": {
		"String": {"HttpTypes", "CurveID_String"},
	},
	"net.HardwareAddr": {
		"String": {"Net", "HardwareAddr_String"},
	},
	"net.Flags": {
		"String": {"Net", "Flags_String"},
	},
	"net.IPNet": {
		"Contains": {"Net", "IPNet_Contains"}, "String": {"Net", "IPNet_String"}, "Network": {"Net", "IPNet_Network"},
	},
	"net.OpError": {
		"Error": {"Net", "OpError_Error"}, "Unwrap": {"Net", "OpError_Unwrap"}, "Timeout": {"Net", "OpError_Timeout"}, "Temporary": {"Net", "OpError_Temporary"},
	},
	"os.SyscallError": {
		"Error": {"Os", "SyscallError_Error"}, "Unwrap": {"Os", "SyscallError_Unwrap"}, "Timeout": {"Os", "SyscallError_Timeout"},
	},
	"os.FileInfo": {
		"Name": {"Os", "FileInfo_Name"}, "Size": {"Os", "FileInfo_Size"}, "IsDir": {"Os", "FileInfo_IsDir"}, "Mode": {"Os", "FileInfo_Mode"}, "ModTime": {"Os", "FileInfo_ModTime"},
	},
	"io/fs.FileInfo": {
		"Name": {"Os", "FileInfo_Name"}, "Size": {"Os", "FileInfo_Size"}, "IsDir": {"Os", "FileInfo_IsDir"}, "Mode": {"Os", "FileInfo_Mode"}, "ModTime": {"Os", "FileInfo_ModTime"},
	},
	"io/fs.DirEntry": {
		"Name": {"Fs", "DirEntry_Name"}, "IsDir": {"Fs", "DirEntry_IsDir"}, "Type": {"Fs", "DirEntry_Type"}, "Info": {"Fs", "DirEntry_Info"},
	},
	"io/fs.FileMode": {
		"Type": {"Fs", "Mode_Type"}, "IsDir": {"Fs", "Mode_IsDir"}, "IsRegular": {"Fs", "Mode_IsRegular"}, "Perm": {"Fs", "Mode_Perm"},
		"String": {"Fs", "Mode_String"},
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
		"And": {"AtomicInt", "Int_And"}, "Or": {"AtomicInt", "Int_Or"},
	},
	"sync/atomic.Int32": {
		"Load": {"AtomicInt", "Int32_Load"}, "Store": {"AtomicInt", "Int32_Store"}, "Add": {"AtomicInt", "Int32_Add"}, "Swap": {"AtomicInt", "Int32_Swap"}, "CompareAndSwap": {"AtomicInt", "Int32_CompareAndSwap"},
		"And": {"AtomicInt", "Int32_And"}, "Or": {"AtomicInt", "Int32_Or"},
	},
	"sync/atomic.Uint64": {
		"Load": {"AtomicInt", "Uint_Load"}, "Store": {"AtomicInt", "Uint_Store"}, "Add": {"AtomicInt", "Uint_Add"}, "Swap": {"AtomicInt", "Uint_Swap"}, "CompareAndSwap": {"AtomicInt", "Uint_CompareAndSwap"},
		"And": {"AtomicInt", "Uint_And"}, "Or": {"AtomicInt", "Uint_Or"},
	},
	"sync/atomic.Uint32": {
		"Load": {"AtomicInt", "Uint32_Load"}, "Store": {"AtomicInt", "Uint32_Store"}, "Add": {"AtomicInt", "Uint32_Add"}, "Swap": {"AtomicInt", "Uint32_Swap"}, "CompareAndSwap": {"AtomicInt", "Uint32_CompareAndSwap"},
		"And": {"AtomicInt", "Uint32_And"}, "Or": {"AtomicInt", "Uint32_Or"},
	},
	"sync/atomic.Uintptr": {
		"Load": {"AtomicInt", "Uint_Load"}, "Store": {"AtomicInt", "Uint_Store"}, "Add": {"AtomicInt", "Uint_Add"}, "Swap": {"AtomicInt", "Uint_Swap"}, "CompareAndSwap": {"AtomicInt", "Uint_CompareAndSwap"},
		"And": {"AtomicInt", "Uint_And"}, "Or": {"AtomicInt", "Uint_Or"},
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
		"Add": {"Sync", "WaitGroup_Add"}, "Done": {"Sync", "WaitGroup_Done"}, "Wait": {"Sync", "WaitGroup_Wait"}, "Go": {"Sync", "WaitGroup_Go"},
	},
	"sync.Once": {
		"Do": {"Sync", "Once_Do"},
	},
	"sync.Map": {
		"Store": {"Sync", "Map_Store"}, "Load": {"Sync", "Map_Load"}, "Delete": {"Sync", "Map_Delete"},
		"LoadOrStore": {"Sync", "Map_LoadOrStore"}, "LoadAndDelete": {"Sync", "Map_LoadAndDelete"}, "Range": {"Sync", "Map_Range"},
		"Swap": {"Sync", "Map_Swap"}, "CompareAndSwap": {"Sync", "Map_CompareAndSwap"}, "CompareAndDelete": {"Sync", "Map_CompareAndDelete"}, "Clear": {"Sync", "Map_Clear"},
	},
	"sync.Pool": {
		"Get": {"Sync", "Pool_Get"}, "Put": {"Sync", "Pool_Put"},
	},
	"math/rand/v2.PCG": {
		"Uint64": {"Rand2", "PCG_Uint64"}, "Seed": {"Rand2", "PCG_Seed"},
		"MarshalBinary": {"Rand2", "PCG_MarshalBinary"}, "AppendBinary": {"Rand2", "PCG_AppendBinary"}, "UnmarshalBinary": {"Rand2", "PCG_UnmarshalBinary"},
	},
	"math/rand/v2.ChaCha8": {
		"Uint64": {"Rand2", "ChaCha8_Uint64"}, "Seed": {"Rand2", "ChaCha8_Seed"},
	},
	"math/rand/v2.Rand": {
		"Uint64": {"Rand2", "RandV2_Uint64"}, "Int64": {"Rand2", "RandV2_Int64"}, "Uint32": {"Rand2", "RandV2_Uint32"},
		"Int32": {"Rand2", "RandV2_Int32"}, "Int": {"Rand2", "RandV2_Int"}, "Uint": {"Rand2", "RandV2_Uint"},
		"Uint64N": {"Rand2", "RandV2_Uint64N"}, "Int64N": {"Rand2", "RandV2_Int64N"}, "Uint32N": {"Rand2", "RandV2_Uint32N"},
		"Int32N": {"Rand2", "RandV2_Int32N"}, "IntN": {"Rand2", "RandV2_IntN"}, "UintN": {"Rand2", "RandV2_UintN"},
		"Float64": {"Rand2", "RandV2_Float64"}, "Float32": {"Rand2", "RandV2_Float32"},
		"NormFloat64": {"Rand2", "RandV2_NormFloat64"}, "ExpFloat64": {"Rand2", "RandV2_ExpFloat64"},
		"Perm": {"Rand2", "RandV2_Perm"}, "Shuffle": {"Rand2", "RandV2_Shuffle"},
	},
	"math/rand.Rand": {
		"Int63": {"Rand", "Rand_Int63"}, "Int": {"Rand", "Rand_Int"}, "Int63n": {"Rand", "Rand_Int63n"},
		"Intn": {"Rand", "Rand_Intn"}, "Float64": {"Rand", "Rand_Float64"}, "Perm": {"Rand", "Rand_Perm"},
		"Shuffle": {"Rand", "Rand_Shuffle"}, "NormFloat64": {"Rand", "Rand_NormFloat64"}, "ExpFloat64": {"Rand", "Rand_ExpFloat64"},
		"Uint64": {"Rand", "Rand_Uint64"}, "Uint32": {"Rand", "Rand_Uint32"}, "Int31": {"Rand", "Rand_Int31"},
		"Int31n": {"Rand", "Rand_Int31n"}, "Float32": {"Rand", "Rand_Float32"}, "Read": {"Rand", "Rand_Read"}, "Seed": {"Rand", "Rand_Seed"},
	},
	"math/rand.Zipf": {"Uint64": {"Rand", "Zipf_Uint64"}},
	"net/netip.Addr": {
		"String": {"Netip", "Addr_String"}, "Is4": {"Netip", "Addr_Is4"}, "Is6": {"Netip", "Addr_Is6"},
		"Is4In6": {"Netip", "Addr_Is4In6"}, "IsValid": {"Netip", "Addr_IsValid"}, "BitLen": {"Netip", "Addr_BitLen"},
		"As4": {"Netip", "Addr_As4"}, "As16": {"Netip", "Addr_As16"}, "AsSlice": {"Netip", "Addr_AsSlice"},
		"Unmap": {"Netip", "Addr_Unmap"}, "Zone": {"Netip", "Addr_Zone"}, "Compare": {"Netip", "Addr_Compare"},
		"Less": {"Netip", "Addr_Less"}, "Next": {"Netip", "Addr_Next"}, "Prev": {"Netip", "Addr_Prev"},
		"IsUnspecified": {"Netip", "Addr_IsUnspecified"}, "IsLoopback": {"Netip", "Addr_IsLoopback"},
		"IsMulticast": {"Netip", "Addr_IsMulticast"}, "IsGlobalUnicast": {"Netip", "Addr_IsGlobalUnicast"},
		"IsLinkLocalUnicast": {"Netip", "Addr_IsLinkLocalUnicast"}, "IsLinkLocalMulticast": {"Netip", "Addr_IsLinkLocalMulticast"},
		"IsInterfaceLocalMulticast": {"Netip", "Addr_IsInterfaceLocalMulticast"}, "IsPrivate": {"Netip", "Addr_IsPrivate"},
		"WithZone": {"Netip", "Addr_WithZone"}, "StringExpanded": {"Netip", "Addr_StringExpanded"},
		"AppendTo": {"Netip", "Addr_AppendTo"}, "AppendText": {"Netip", "Addr_AppendText"}, "MarshalText": {"Netip", "Addr_MarshalText"},
		"AppendBinary": {"Netip", "Addr_AppendBinary"}, "MarshalBinary": {"Netip", "Addr_MarshalBinary"},
		"UnmarshalText": {"Netip", "Addr_UnmarshalText"}, "UnmarshalBinary": {"Netip", "Addr_UnmarshalBinary"},
		"Prefix": {"Netip", "Addr_Prefix"},
	},
	"net/netip.AddrPort": {
		"Addr": {"Netip", "AddrPort_Addr"}, "Port": {"Netip", "AddrPort_Port"}, "IsValid": {"Netip", "AddrPort_IsValid"},
		"Compare": {"Netip", "AddrPort_Compare"}, "String": {"Netip", "AddrPort_String"}, "AppendTo": {"Netip", "AddrPort_AppendTo"},
		"AppendText": {"Netip", "AddrPort_AppendText"}, "MarshalText": {"Netip", "AddrPort_MarshalText"}, "UnmarshalText": {"Netip", "AddrPort_UnmarshalText"},
		"AppendBinary": {"Netip", "AddrPort_AppendBinary"}, "MarshalBinary": {"Netip", "AddrPort_MarshalBinary"}, "UnmarshalBinary": {"Netip", "AddrPort_UnmarshalBinary"},
	},
	"net/netip.Prefix": {
		"Addr": {"Netip", "Prefix_Addr"}, "Bits": {"Netip", "Prefix_Bits"}, "IsValid": {"Netip", "Prefix_IsValid"},
		"IsSingleIP": {"Netip", "Prefix_IsSingleIP"}, "Masked": {"Netip", "Prefix_Masked"}, "Compare": {"Netip", "Prefix_Compare"},
		"Contains": {"Netip", "Prefix_Contains"}, "Overlaps": {"Netip", "Prefix_Overlaps"}, "String": {"Netip", "Prefix_String"},
		"AppendTo": {"Netip", "Prefix_AppendTo"},
		"AppendText": {"Netip", "Prefix_AppendText"}, "MarshalText": {"Netip", "Prefix_MarshalText"}, "UnmarshalText": {"Netip", "Prefix_UnmarshalText"},
		"AppendBinary": {"Netip", "Prefix_AppendBinary"}, "MarshalBinary": {"Netip", "Prefix_MarshalBinary"}, "UnmarshalBinary": {"Netip", "Prefix_UnmarshalBinary"},
	},
	"math/big.Accuracy":   {"String": {"Big", "Accuracy_String"}},
	"math/big.RoundingMode": {"String": {"Big", "RoundingMode_String"}},
	"flag.Value":            {"String": {"Flag", "Value_String"}, "Set": {"Flag", "Value_Set"}},
	"flag.FlagSet": {
		"Bool": {"Flag", "FS_Bool"}, "Int": {"Flag", "FS_Int"}, "Int64": {"Flag", "FS_Int64"}, "Uint": {"Flag", "FS_Uint"},
		"Uint64": {"Flag", "FS_Uint64"}, "Float64": {"Flag", "FS_Float64"}, "String": {"Flag", "FS_String"}, "Duration": {"Flag", "FS_Duration"},
		"BoolVar": {"Flag", "FS_BoolVar"}, "IntVar": {"Flag", "FS_IntVar"}, "Int64Var": {"Flag", "FS_Int64Var"}, "UintVar": {"Flag", "FS_UintVar"},
		"Uint64Var": {"Flag", "FS_Uint64Var"}, "Float64Var": {"Flag", "FS_Float64Var"}, "StringVar": {"Flag", "FS_StringVar"}, "DurationVar": {"Flag", "FS_DurationVar"},
		"Parse": {"Flag", "FS_Parse"}, "Parsed": {"Flag", "FS_Parsed"}, "Set": {"Flag", "FS_Set"}, "Name": {"Flag", "FS_Name"},
		"NArg": {"Flag", "FS_NArg"}, "NFlag": {"Flag", "FS_NFlag"}, "Arg": {"Flag", "FS_Arg"}, "Args": {"Flag", "FS_Args"},
		"ErrorHandling": {"Flag", "FS_ErrorHandling"},
		"Lookup": {"Flag", "FS_Lookup"}, "Visit": {"Flag", "FS_Visit"}, "VisitAll": {"Flag", "FS_VisitAll"},
		"Func": {"Flag", "FS_Func"}, "BoolFunc": {"Flag", "FS_BoolFunc"}, "Var": {"Flag", "FS_Var"},
		"Init": {"Flag", "FS_Init"}, "Output": {"Flag", "FS_Output"}, "SetOutput": {"Flag", "FS_SetOutput"},
		"PrintDefaults": {"Flag", "FS_PrintDefaults"},
	},
	"context.Context": {
		"Value": {"Context", "Context_Value"}, "Err": {"Context", "Context_Err"},
		"Done": {"Context", "Context_Done"}, "Deadline": {"Context", "Context_Deadline"},
	},
	"time.Time": {
		"Unix": {"Time", "Time_Unix"}, "UnixNano": {"Time", "Time_UnixNano"}, "UnixMilli": {"Time", "Time_UnixMilli"},
		"Year": {"Time", "Time_Year"}, "Month": {"Time", "Time_Month"}, "Day": {"Time", "Time_Day"},
		"Date": {"Time", "Time_Date"}, "Clock": {"Time", "Time_Clock"},
		"Hour": {"Time", "Time_Hour"}, "Minute": {"Time", "Time_Minute"}, "Second": {"Time", "Time_Second"},
		"Nanosecond": {"Time", "Time_Nanosecond"}, "Weekday": {"Time", "Time_Weekday"},
		"Add": {"Time", "Time_Add"}, "AddDate": {"Time", "Time_AddDate"}, "Sub": {"Time", "Time_Sub"}, "Round": {"Time", "Time_Round"}, "Truncate": {"Time", "Time_Truncate"},
		"Before": {"Time", "Time_Before"}, "After": {"Time", "Time_After"}, "Equal": {"Time", "Time_Equal"},
		"IsZero": {"Time", "Time_IsZero"}, "UTC": {"Time", "Time_UTC"}, "Local": {"Time", "Time_Local"},
		"String": {"Time", "Time_String"}, "Format": {"Time", "Time_Format"}, "AppendFormat": {"Time", "Time_AppendFormat"},
		"Zone": {"Time", "Time_Zone"}, "YearDay": {"Time", "Time_YearDay"}, "In": {"Time", "Time_In"}, "Location": {"Time", "Time_Location"},
		"Compare": {"Time", "Time_Compare"}, "UnixMicro": {"Time", "Time_UnixMicro"}, "ISOWeek": {"Time", "Time_ISOWeek"},
		"IsDST": {"Time", "Time_IsDST"}, "GoString": {"Time", "Time_GoString"},
		"MarshalText": {"Time", "Time_MarshalText"}, "AppendText": {"Time", "Time_AppendText"}, "MarshalJSON": {"Time", "Time_MarshalJSON"},
		"MarshalBinary": {"Time", "Time_MarshalBinary"}, "AppendBinary": {"Time", "Time_AppendBinary"}, "GobEncode": {"Time", "Time_GobEncode"},
		"UnmarshalText": {"Time", "Time_UnmarshalText"}, "UnmarshalJSON": {"Time", "Time_UnmarshalJSON"},
		"UnmarshalBinary": {"Time", "Time_UnmarshalBinary"}, "GobDecode": {"Time", "Time_GobDecode"}, "ZoneBounds": {"Time", "Time_ZoneBounds"},
	},
	"time.Location": {
		"String": {"Time", "Location_String"},
	},
	"log/slog.Level": {
		"String": {"Slog", "Level_String"}, "Level": {"Slog", "Level_Level"},
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
		"Abs": {"Time", "Duration_Abs"},
	},
	"reflect.ChanDir": {
		"String": {"Reflect", "ChanDir_String"},
	},
	"reflect.Value": {
		"Kind": {"Reflect", "Value_Kind"}, "Type": {"Reflect", "Value_Type"},
		"Comparable": {"Reflect", "Value_Comparable"}, "Equal": {"Reflect", "Value_Equal"},
		"CanInt": {"Reflect", "Value_CanInt"}, "CanUint": {"Reflect", "Value_CanUint"}, "CanFloat": {"Reflect", "Value_CanFloat"}, "CanComplex": {"Reflect", "Value_CanComplex"},
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
		"SetMapIndex": {"Reflect", "Value_SetMapIndex"}, "Convert": {"Reflect", "Value_Convert"}, "CanConvert": {"Reflect", "Value_CanConvert"},
		"Addr": {"Reflect", "Value_Addr"}, "Cap": {"Reflect", "Value_Cap"},
		"SetLen": {"Reflect", "Value_SetLen"}, "SetCap": {"Reflect", "Value_SetCap"},
		"Slice": {"Reflect", "Value_Slice"}, "Slice3": {"Reflect", "Value_Slice3"}, "SetBytes": {"Reflect", "Value_SetBytes"}, "SetComplex": {"Reflect", "Value_SetComplex"},
		"Clear": {"Reflect", "Value_Clear"}, "Grow": {"Reflect", "Value_Grow"}, "OverflowComplex": {"Reflect", "Value_OverflowComplex"},
		"MapRange": {"Reflect", "Value_MapRange"}, "SetIterKey": {"Reflect", "Value_SetIterKey"}, "SetIterValue": {"Reflect", "Value_SetIterValue"},
		"FieldByIndexErr": {"Reflect", "Value_FieldByIndexErr"}, "FieldByNameFunc": {"Reflect", "Value_FieldByNameFunc"},
		"Pointer": {"Reflect", "Value_Pointer"},
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
	// A method call on an interface-typed receiver that ALSO has a user (lowered) implementer
	// must NOT short-circuit to the shim extern — that casts the value to the one shim handle
	// and crashes on the user's own type (the net.Listener / GoFileInfo bug, e.g. a user
	// fs.FileInfo). Route it through interfaceDispatch, which enumerates both the user
	// implementers AND the shim handle (via shimIfaceImplementers + the [GoShim] class).
	// When the only implementers are shim handles (hash.Hash, context.Context, …) the
	// short-circuit below is correct and faster, so keep it.
	if iface, isIface := seln.Recv().Underlying().(*types.Interface); isIface {
		// Only a NON-stdlib-from-source implementer should flip this to interface
		// dispatch. A compileFromSource stdlib package (e.g. io, now lowered) contributes
		// internal types like io.nopCloser that implement io.ReadCloser but never flow as
		// the receiver of a shim-backed interface field (an http response Body is always a
		// GoReader handle): counting them would route resp.Body.Close() through
		// interfaceDispatch, where the GoReader matches no enumerated implementer and the
		// call nil-panics. User implementers (and other lowered app types) still suppress
		// the short-circuit, preserving the net.Listener / GoFileInfo fix.
		for _, impl := range l.implementers(iface) {
			if pkg := impl.named.Obj().Pkg(); pkg == nil || !compileFromSource[pkg.Path()] {
				return nil, false
			}
		}
	}
	// For a method promoted from an embedded shim field (struct{ *sha3.SHAKE }),
	// seln.Recv() is the outer struct, so key the registry on the method's own
	// receiver type instead. Only do this when actually promoted (index depth > 1) to
	// leave the direct-call path — and its receiver IR typing — exactly as before.
	recv := namedOf(seln.Recv())
	if len(seln.Index()) > 1 {
		// A promoted method keys to the method's own receiver (an embedded shim field
		// like sha3.SHAKE or sync.Mutex) — UNLESS the outer type is itself a registered
		// shim that defines this method directly. An opaque shim such as net.UDPConn is
		// one runtime handle whose methods are promoted from an unexported embedded type
		// (net.conn); those stay keyed to the outer type, and emitEmbedNav is a no-op on
		// an opaque (non-struct) receiver, so the handle is passed through unchanged.
		outerHas := false
		if recv != nil && recv.Obj() != nil && recv.Obj().Pkg() != nil {
			_, outerHas = shimMethodRegistry[recv.Obj().Pkg().Path()+"."+recv.Obj().Name()][fn.Name()]
		}
		if !outerHas {
			if mr := namedOf(fn.Type().(*types.Signature).Recv().Type()); mr != nil {
				recv = mr
			}
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
	// A variadic shim method (e.g. slog.Logger.Info(msg, args ...any)) must pack the
	// trailing arguments into the final slice parameter, like the free-function path.
	if sig, ok := seln.Obj().(*types.Func); ok {
		if s, ok := sig.Type().(*types.Signature); ok && s.Variadic() {
			nFixed := s.Params().Len() - 1
			for i := 0; i < nFixed; i++ {
				l.exprCoerced(e.Args[i], ext.Params[i+1])
			}
			sliceParam := ext.Params[len(ext.Params)-1]
			if e.Ellipsis.IsValid() {
				l.exprCoerced(e.Args[nFixed], sliceParam) // m.Info(slice...) — passed directly
			} else {
				l.packVariadic(e.Args[nFixed:], *sliceParam.Elem)
			}
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
			return ext.Ret
		}
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

// containsTypeParam reports whether t is, or structurally contains, a type parameter — the
// signal that a generic shim's result lowers to a boxed object needing an unbox at the call.
func containsTypeParam(t types.Type) bool {
	switch x := t.(type) {
	case *types.TypeParam:
		return true
	case *types.Slice:
		return containsTypeParam(x.Elem())
	case *types.Array:
		return containsTypeParam(x.Elem())
	case *types.Pointer:
		return containsTypeParam(x.Elem())
	case *types.Map:
		return containsTypeParam(x.Key()) || containsTypeParam(x.Elem())
	case *types.Chan:
		return containsTypeParam(x.Elem())
	}
	return false
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
		rtype := sig.Results().At(0).Type()
		if containsTypeParam(rtype) {
			// A generic result (slices.Max -> E, slices.Clone -> []E): the C# shim returns a
			// boxed object; shimCall unboxes it to the call's instantiated type.
			ret = goir.TObject
		} else if ret, ok = l.lowerCtx.goType(rtype); !ok {
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
	// A generic shim whose result is a type parameter (slices.Max -> E, slices.Clone -> []E)
	// lowers its result to a boxed object (ext.Ret == TObject). If the call's *instantiated*
	// result type is a concrete CLR type, unbox it so the caller sees the real value. A shim
	// that genuinely returns `any` keeps an interface instantiated type, so it stays boxed.
	if ext.Ret.Kind == goir.KObject && ext.Ret.Shim == "" {
		if rt := l.pkg.TypesInfo.TypeOf(e); rt != nil {
			if ct, ok := l.lowerCtx.goType(rt); ok && ct.Kind != goir.KObject {
				l.emitUnbox(ct)
				return ct
			}
		}
	}
	return ext.Ret
}
