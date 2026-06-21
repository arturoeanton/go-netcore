package emit

import (
	"fmt"
	"os"
	"strings"
	"sync"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// Shim-signature manifest: when GOCLR_SHIM_MANIFEST names a file, every distinct extern
// the program emits is appended as a line
//
//	Assembly\tType\tMethod\tparam1,param2,…\tret
//
// where each type is the CLR type the IL signature blob actually encodes (see
// appendTypeSig — the source of truth the CLR matches against the target method at JIT).
// The Tier-2 shim validator (internal/lower/shim_signatures_test.go) parses this and the
// runtime C# sources and asserts they agree, catching return/parameter mismatches a
// program would otherwise only hit as a MissingMethodException at run time.
var (
	manifestOnce sync.Once
	manifestFile *os.File
)

func manifestSink() *os.File {
	manifestOnce.Do(func() {
		if p := os.Getenv("GOCLR_SHIM_MANIFEST"); p != "" {
			// Append so builds across a corpus accumulate into one manifest.
			manifestFile, _ = os.OpenFile(p, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0o644)
		}
	})
	return manifestFile
}

// clrName renders the CLR type a goir.Type encodes to in a method signature, matching
// appendTypeSig exactly.
func clrName(t goir.Type) string {
	switch t.Kind {
	case goir.KVoid:
		return "void"
	case goir.KInt64:
		return "long"
	case goir.KInt32:
		return "int"
	case goir.KUint64:
		return "ulong"
	case goir.KUint32:
		return "uint"
	case goir.KFloat64:
		return "double"
	case goir.KFloat32:
		return "float"
	case goir.KBool:
		return "bool"
	case goir.KString:
		return "GoString"
	case goir.KSlice:
		return "GoSlice"
	case goir.KMap:
		return "GoMap"
	case goir.KPtr:
		return "GoPtr"
	case goir.KFunc:
		return "GoClosure"
	case goir.KChan:
		return "GoChan"
	case goir.KComplex:
		return "GoComplex"
	case goir.KObjectArray:
		return "object[]"
	case goir.KObject:
		return "object"
	case goir.KStruct:
		return "struct" // a user struct value type; the validator does not type-match these
	default:
		return "object"
	}
}

// recordExtern appends one extern to the manifest, if enabled.
func recordExtern(e *goir.Extern) {
	f := manifestSink()
	if f == nil {
		return
	}
	params := make([]string, len(e.Params))
	for i, p := range e.Params {
		params[i] = clrName(p)
	}
	fmt.Fprintf(f, "%s\t%s\t%s\t%s\t%s\n", e.Assembly, e.Type, e.Method, strings.Join(params, ","), clrName(e.Ret))
}
