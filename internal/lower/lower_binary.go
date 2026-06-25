package lower

import (
	"go/ast"
	"go/types"
	"strconv"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// binaryDescriptor encodes the static type of a binary.Write/Read/Size argument as a
// compact string the runtime uses to serialize with Go's exact field widths — goclr
// boxes a uint16/uint8 as a 32-bit int, so the runtime cannot recover the width from
// the value alone. Returns "" for a type binary cannot encode (matching Go's error).
//
// Codes: b bool, c int8, C uint8, s int16, S uint16, i int32, I uint32, f float32,
// l int64, L uint64, g float64; struct {fields}; array [N,elem]; slice <elem>.
func binaryDescriptor(t types.Type) string {
	switch u := t.Underlying().(type) {
	case *types.Basic:
		switch u.Kind() {
		case types.Bool:
			return "b"
		case types.Int8:
			return "c"
		case types.Uint8:
			return "C"
		case types.Int16:
			return "s"
		case types.Uint16:
			return "S"
		case types.Int32:
			return "i"
		case types.Uint32:
			return "I"
		case types.Int64:
			return "l"
		case types.Uint64:
			return "L"
		case types.Float32:
			return "f"
		case types.Float64:
			return "g"
		}
		return "" // int/uint (machine-dependent), string, complex — not fixed-size
	case *types.Struct:
		var sb strings.Builder
		sb.WriteByte('{')
		for i := 0; i < u.NumFields(); i++ {
			d := binaryDescriptor(u.Field(i).Type())
			if d == "" {
				return ""
			}
			sb.WriteString(d)
		}
		sb.WriteByte('}')
		return sb.String()
	case *types.Array:
		d := binaryDescriptor(u.Elem())
		if d == "" {
			return ""
		}
		return "[" + strconv.FormatInt(u.Len(), 10) + "," + d + "]"
	case *types.Slice:
		d := binaryDescriptor(u.Elem())
		if d == "" {
			return ""
		}
		return "<" + d + ">"
	}
	return ""
}

// binaryWriteCall lowers binary.Write(w, order, data): it passes data's static-type
// descriptor so the runtime serializes each field at Go's width.
func (l *funcLowerer) binaryWriteCall(e *ast.CallExpr) goir.Type {
	desc := binaryDescriptor(l.pkg.TypesInfo.TypeOf(e.Args[2]))
	if desc == "" {
		// Not a fixed-size type goclr describes: fall back to the legacy value-based call
		// (correct for wide ints; reports binary's own error otherwise).
		l.expr(e.Args[0])
		l.expr(e.Args[1])
		l.emitBoxedElem(e.Args[2])
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Binary", Method: "Write",
			Params: []goir.Type{goir.TObject, goir.TObject, goir.TObject}, Ret: goir.TObject,
		}})
		return goir.TObject
	}
	l.expr(e.Args[0])           // w
	l.expr(e.Args[1])           // order
	l.emitBoxedElem(e.Args[2])  // data (boxed)
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Binary", Method: "WriteDesc",
		Params: []goir.Type{goir.TObject, goir.TObject, goir.TObject, goir.TString}, Ret: goir.TObject,
	}})
	return goir.TObject // error
}

// binaryReadCall lowers binary.Read(r, order, &v): passes the pointee's descriptor so
// the runtime reconstructs each field at Go's width and writes it back through the cell.
func (l *funcLowerer) binaryReadCall(e *ast.CallExpr) goir.Type {
	desc := ""
	if pt, ok := l.pkg.TypesInfo.TypeOf(e.Args[2]).Underlying().(*types.Pointer); ok {
		desc = binaryDescriptor(pt.Elem())
	}
	if desc == "" {
		l.expr(e.Args[0])
		l.expr(e.Args[1])
		l.expr(e.Args[2])
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Binary", Method: "Read",
			Params: []goir.Type{goir.TObject, goir.TObject, goir.TObject}, Ret: goir.TObject,
		}})
		return goir.TObject
	}
	l.expr(e.Args[0]) // r
	l.expr(e.Args[1]) // order
	l.expr(e.Args[2]) // the GoPtr target
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Binary", Method: "ReadDesc",
		Params: []goir.Type{goir.TObject, goir.TObject, goir.TObject, goir.TString}, Ret: goir.TObject,
	}})
	return goir.TObject // error
}

// binarySizeCall lowers binary.Size(data): passes the descriptor so the byte count
// uses Go's widths.
func (l *funcLowerer) binarySizeCall(e *ast.CallExpr) goir.Type {
	desc := binaryDescriptor(l.pkg.TypesInfo.TypeOf(e.Args[0]))
	if desc == "" {
		l.emitBoxedElem(e.Args[0])
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Binary", Method: "Size",
			Params: []goir.Type{goir.TObject}, Ret: goir.TInt64,
		}})
		return goir.TInt64
	}
	l.emitBoxedElem(e.Args[0])
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Binary", Method: "SizeDesc",
		Params: []goir.Type{goir.TObject, goir.TString}, Ret: goir.TInt64,
	}})
	return goir.TInt64
}
