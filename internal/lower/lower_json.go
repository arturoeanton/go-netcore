package lower

import (
	"go/ast"
	"go/types"
	"strconv"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// errorsAsCall lowers errors.As(err, &target). The runtime erases the target's
// static type, so the matched concrete type's CLR name is passed as a descriptor;
// the shim walks the Unwrap chain and assigns the first matching error to *target.
func (l *funcLowerer) errorsAsCall(e *ast.CallExpr) goir.Type {
	if len(e.Args) != 2 {
		l.fail(e.Pos(), "errors.As arguments")
		return goir.TVoid
	}
	desc := ""
	if pt, ok := l.pkg.TypesInfo.TypeOf(e.Args[1]).Underlying().(*types.Pointer); ok {
		desc = l.errorMatchName(pt.Elem()) // matched type T (target is *T)
	}
	ext := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Errors", Method: "As",
		Params: []goir.Type{goir.TObject, goir.TObject, goir.TString}, Ret: goir.TBool,
	}
	l.exprCoerced(e.Args[0], goir.TObject) // err
	l.expr(e.Args[1])                      // *target (a GoPtr)
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TBool
}

// errorMatchName returns the CLR type name the errors.As shim compares chain
// errors against: the named struct behind a *T or T target element type.
func (l *funcLowerer) errorMatchName(t types.Type) string {
	if p, ok := t.Underlying().(*types.Pointer); ok {
		t = p.Elem()
	}
	if named, ok := t.(*types.Named); ok {
		if _, isStruct := named.Underlying().(*types.Struct); isStruct {
			return l.structFor(named).Name
		}
	}
	return ""
}

// jsonUnmarshalCall lowers encoding/json.Unmarshal(data, &v). Because the runtime
// slice/map representation erases element types, the static type of the target is
// encoded into a descriptor string (compact JSON) built here and consumed by the
// C# decoder, which writes the decoded value back through the GoPtr cell.
func (l *funcLowerer) jsonUnmarshalCall(e *ast.CallExpr) goir.Type {
	if len(e.Args) != 2 {
		l.fail(e.Pos(), "json.Unmarshal arguments")
		return goir.TVoid
	}
	desc := "{\"k\":\"any\"}"
	if pt, ok := l.pkg.TypesInfo.TypeOf(e.Args[1]).Underlying().(*types.Pointer); ok {
		desc = l.jsonDescriptor(pt.Elem(), map[string]bool{})
	}
	dataT, ok := l.goType(l.pkg.TypesInfo.TypeOf(e.Args[0]))
	if !ok {
		dataT = goir.TObject
	}
	ext := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Json", Method: "Unmarshal",
		Params: []goir.Type{dataT, goir.TObject, goir.TString}, Ret: goir.TObject,
	}
	l.exprCoerced(e.Args[0], dataT) // []byte data
	l.expr(e.Args[1])               // the GoPtr target (reference -> object)
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TObject // error
}

// jsonDecoderDecode lowers dec.Decode(&v): it passes the target's static-type
// descriptor (as json.Unmarshal does) so the next value decodes into the concrete
// struct/slice/map instead of a generic map the cast to the target would reject.
func (l *funcLowerer) jsonDecoderDecode(e *ast.CallExpr, sel *ast.SelectorExpr) goir.Type {
	desc := "{\"k\":\"any\"}"
	if pt, ok := l.pkg.TypesInfo.TypeOf(e.Args[0]).Underlying().(*types.Pointer); ok {
		desc = l.jsonDescriptor(pt.Elem(), map[string]bool{})
	}
	ext := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Json", Method: "Decoder_DecodeTyped",
		Params: []goir.Type{goir.TObject, goir.TObject, goir.TString}, Ret: goir.TObject,
	}
	l.expr(sel.X)         // the *json.Decoder receiver
	l.expr(e.Args[0])     // the GoPtr target
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TObject // error
}

// jsonDescriptor builds the compact JSON type descriptor consumed by the C#
// json decoder. seen guards against recursive struct types.
func (l *funcLowerer) jsonDescriptor(t types.Type, seen map[string]bool) string {
	switch u := t.Underlying().(type) {
	case *types.Basic:
		switch {
		case u.Info()&types.IsBoolean != 0:
			return `{"k":"bool"}`
		case u.Info()&types.IsUnsigned != 0:
			return `{"k":"uint"}`
		case u.Info()&types.IsInteger != 0:
			return `{"k":"int"}`
		case u.Info()&types.IsFloat != 0:
			return `{"k":"float"}`
		case u.Info()&types.IsString != 0:
			return `{"k":"string"}`
		}
		return `{"k":"any"}`
	case *types.Pointer:
		return `{"k":"ptr","e":` + l.jsonDescriptor(u.Elem(), seen) + `}`
	case *types.Slice:
		// []byte decodes from a base64 string in Go; flag it.
		if b, ok := u.Elem().Underlying().(*types.Basic); ok && b.Kind() == types.Byte {
			return `{"k":"bytes"}`
		}
		return `{"k":"slice","e":` + l.jsonDescriptor(u.Elem(), seen) + `}`
	case *types.Map:
		return `{"k":"map","v":` + l.jsonDescriptor(u.Elem(), seen) + `}`
	case *types.Struct:
		// Named struct -> its registered descriptor; an anonymous struct (a common
		// request-binding shape, `var in struct{ ... }`) gets its synthesized CLR type.
		var s *goir.Struct
		if named, ok := t.(*types.Named); ok {
			s = l.structFor(named)
		} else {
			s = l.structForAnon(u)
		}
		if seen[s.Name] {
			return `{"k":"ptr","e":{"k":"any"}}` // break cycles defensively
		}
		seen[s.Name] = true
		var b strings.Builder
		b.WriteString(`{"k":"struct","n":`)
		b.WriteString(strconv.Quote(s.Name))
		b.WriteString(`,"f":[`)
		first := true
		for i := 0; i < u.NumFields(); i++ {
			f := u.Field(i)
			if !f.Exported() {
				continue
			}
			key, skip := jsonFieldKey(f.Name(), u.Tag(i))
			if skip {
				continue
			}
			if !first {
				b.WriteByte(',')
			}
			first = false
			b.WriteString(`{"j":`)
			b.WriteString(strconv.Quote(key))
			b.WriteString(`,"c":`)
			b.WriteString(strconv.Quote(f.Name()))
			b.WriteString(`,"t":`)
			b.WriteString(l.jsonDescriptor(f.Type(), seen))
			b.WriteByte('}')
		}
		b.WriteString(`]}`)
		delete(seen, s.Name)
		return b.String()
	case *types.Interface:
		return `{"k":"any"}`
	}
	return `{"k":"any"}`
}

// jsonFieldKey returns the JSON object key for a struct field and whether the
// field is skipped (`json:"-"`).
func jsonFieldKey(name, tag string) (string, bool) {
	jt, ok := structTagLookup(tag, "json")
	if !ok {
		return name, false
	}
	parts := strings.Split(jt, ",")
	if parts[0] == "-" && len(parts) == 1 {
		return "", true
	}
	if parts[0] != "" {
		return parts[0], false
	}
	return name, false
}

// structTagLookup extracts the value for key from a raw struct tag string
// (`json:"x" xml:"y"`), mirroring reflect.StructTag.Lookup.
func structTagLookup(tag, key string) (string, bool) {
	for tag != "" {
		i := 0
		for i < len(tag) && tag[i] == ' ' {
			i++
		}
		tag = tag[i:]
		if tag == "" {
			break
		}
		i = 0
		for i < len(tag) && tag[i] != ':' && tag[i] != '"' && tag[i] != ' ' {
			i++
		}
		if i == 0 || i+1 >= len(tag) || tag[i] != ':' || tag[i+1] != '"' {
			break
		}
		name := tag[:i]
		tag = tag[i+1:]
		// scan the quoted value
		i = 1
		for i < len(tag) && tag[i] != '"' {
			if tag[i] == '\\' {
				i++
			}
			i++
		}
		if i >= len(tag) {
			break
		}
		qval := tag[:i+1]
		tag = tag[i+1:]
		if name == key {
			val, err := strconv.Unquote(qval)
			if err != nil {
				return "", false
			}
			return val, true
		}
	}
	return "", false
}
