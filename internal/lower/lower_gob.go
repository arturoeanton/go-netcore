package lower

import (
	"go/ast"
	"go/types"
	"strconv"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// gobEncoderEncode lowers enc.Encode(v). The runtime erases slice/map element
// types, so the ARGUMENT's static type is passed as a descriptor; the C# shim
// registers gob type ids and emits the wire format from it (byte-exact,
// including the type-definition messages and Go's type-naming rules).
func (l *funcLowerer) gobEncoderEncode(e *ast.CallExpr, sel *ast.SelectorExpr) goir.Type {
	desc := l.gobDescriptor(l.pkg.TypesInfo.TypeOf(e.Args[0]), map[string]bool{})
	ext := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Gob", Method: "Encoder_EncodeTyped",
		Params: []goir.Type{goir.TObject, goir.TObject, goir.TString}, Ret: goir.TObject,
	}
	l.expr(sel.X)                          // the *gob.Encoder receiver
	l.exprCoerced(e.Args[0], goir.TObject) // the value (boxed)
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TObject // error
}

// gobDecoderDecode lowers dec.Decode(&v), passing the target's static-type
// descriptor so the wire value decodes into the concrete local shape.
func (l *funcLowerer) gobDecoderDecode(e *ast.CallExpr, sel *ast.SelectorExpr) goir.Type {
	desc := `{"k":"any"}`
	if pt, ok := l.pkg.TypesInfo.TypeOf(e.Args[0]).Underlying().(*types.Pointer); ok {
		desc = l.gobDescriptor(pt.Elem(), map[string]bool{})
	}
	ext := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Gob", Method: "Decoder_DecodeTyped",
		Params: []goir.Type{goir.TObject, goir.TObject, goir.TString}, Ret: goir.TObject,
	}
	l.expr(sel.X)     // the *gob.Decoder receiver
	l.expr(e.Args[0]) // the GoPtr target
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TObject // error
}

// gobDescriptor builds the compact JSON type descriptor the C# gob shim consumes.
// Each node carries the wire kind plus the two names Go's registry uses: "gn" (the
// named type's unqualified Name(), "" for unnamed) and "ts" (types.TypeString with
// package-name qualifier, Go's typ.String()). Structs also carry the CLR type name
// ("n") and per-field Go/CLR names. seen guards recursive types (they terminate via
// the registry key on the C# side; the descriptor breaks the cycle with a ref node).
func (l *funcLowerer) gobDescriptor(t types.Type, seen map[string]bool) string {
	t = types.Unalias(t)
	gn := ""
	if named, ok := t.(*types.Named); ok {
		gn = named.Obj().Name()
	}
	ts := typeDescStr(t)
	names := `,"gn":` + strconv.Quote(gn) + `,"ts":` + strconv.Quote(ts)
	switch u := t.Underlying().(type) {
	case *types.Basic:
		switch {
		case u.Info()&types.IsBoolean != 0:
			return `{"k":"bool"` + names + `}`
		case u.Info()&types.IsUnsigned != 0:
			return `{"k":"uint"` + names + `}`
		case u.Info()&types.IsInteger != 0:
			return `{"k":"int"` + names + `}`
		case u.Info()&types.IsFloat != 0:
			return `{"k":"float"` + names + `}`
		case u.Info()&types.IsComplex != 0:
			return `{"k":"complex"` + names + `}`
		case u.Info()&types.IsString != 0:
			return `{"k":"string"` + names + `}`
		}
		return `{"k":"any"` + names + `}`
	case *types.Pointer:
		return `{"k":"ptr","e":` + l.gobDescriptor(u.Elem(), seen) + `}`
	case *types.Slice:
		if b, ok := u.Elem().Underlying().(*types.Basic); ok && b.Kind() == types.Byte {
			return `{"k":"bytes"` + names + `}`
		}
		return `{"k":"slice"` + names + `,"e":` + l.gobDescriptor(u.Elem(), seen) + `}`
	case *types.Array:
		return `{"k":"array"` + names + `,"len":` + strconv.FormatInt(u.Len(), 10) +
			`,"e":` + l.gobDescriptor(u.Elem(), seen) + `}`
	case *types.Map:
		return `{"k":"map"` + names + `,"key":` + l.gobDescriptor(u.Key(), seen) +
			`,"v":` + l.gobDescriptor(u.Elem(), seen) + `}`
	case *types.Struct:
		var s *goir.Struct
		if named, ok := t.(*types.Named); ok {
			s = l.structFor(named)
		} else {
			s = l.structForAnon(u)
		}
		if seen[ts] {
			// A recursive struct: reference by names only; the C# registry resolves
			// the cycle through its type-string key (the id is already assigned by
			// the time a recursive reference is encoded).
			return `{"k":"struct"` + names + `,"n":` + strconv.Quote(s.Name) + `,"f":[]}`
		}
		seen[ts] = true
		var b strings.Builder
		b.WriteString(`{"k":"struct"`)
		b.WriteString(names)
		b.WriteString(`,"n":`)
		b.WriteString(strconv.Quote(s.Name))
		b.WriteString(`,"f":[`)
		first := true
		for i := 0; i < u.NumFields(); i++ {
			f := u.Field(i)
			if !f.Exported() || !gobFieldSent(f.Type()) {
				continue
			}
			if !first {
				b.WriteByte(',')
			}
			first = false
			b.WriteString(`{"g":`)
			b.WriteString(strconv.Quote(f.Name()))
			b.WriteString(`,"c":`)
			b.WriteString(strconv.Quote(f.Name()))
			b.WriteString(`,"t":`)
			b.WriteString(l.gobDescriptor(f.Type(), seen))
			b.WriteByte('}')
		}
		b.WriteString(`]}`)
		delete(seen, ts)
		return b.String()
	case *types.Interface:
		return `{"k":"any"` + names + `}`
	}
	return `{"k":"any"` + names + `}`
}

// gobFieldSent mirrors Go's isSent: chan and func fields (and pointers to them)
// are not transmitted.
func gobFieldSent(t types.Type) bool {
	for {
		if p, ok := t.Underlying().(*types.Pointer); ok {
			t = p.Elem()
			continue
		}
		break
	}
	switch t.Underlying().(type) {
	case *types.Chan, *types.Signature:
		return false
	}
	return true
}
