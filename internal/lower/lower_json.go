package lower

import (
	"go/ast"
	"go/token"
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
	// The target must be an aliasing pointer (the local's cell) so the matched error is
	// written back. For a &local target, emit the cell directly — addrOf's "&shim value is
	// itself" shortcut would otherwise pass a shim-struct *value* (not a write-back cell),
	// so errors.As could never assign through it.
	emitted := false
	if u, ok := unparen(e.Args[1]).(*ast.UnaryExpr); ok && u.Op == token.AND {
		emitted = l.emitAddr(u.X)
	}
	if !emitted {
		l.expr(e.Args[1]) // already a *target pointer value
	}
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TBool
}

// errorMatchName returns the type name the errors.As shim compares chain errors against:
// the goclr-generated CLR struct name for a user error, or — for a shim error struct
// (csv.ParseError, os.PathError, …) whose runtime value is a C# [GoShim] class — its Go
// type name, which the runtime resolves through ShimTypes.IsStrict.
func (l *funcLowerer) errorMatchName(t types.Type) string {
	if p, ok := t.Underlying().(*types.Pointer); ok {
		t = p.Elem()
	}
	if named, ok := t.(*types.Named); ok {
		if obj := named.Obj(); obj.Pkg() != nil && opaqueShimTypes[obj.Pkg().Path()+"."+obj.Name()] {
			return obj.Pkg().Path() + "." + obj.Name()
		}
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
	l.expr(sel.X)     // the *json.Decoder receiver
	l.expr(e.Args[0]) // the GoPtr target
	l.emit(goir.Op{Code: goir.OpStrConst, Str: desc})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: ext})
	return goir.TObject // error
}

// jsonDescriptor builds the compact JSON type descriptor consumed by the C#
// json decoder. seen guards against recursive struct types.
func (l *funcLowerer) jsonDescriptor(t types.Type, seen map[string]bool) string {
	// encoding/json's special named types: Number (raw numeric text kept as a string)
	// and RawMessage (the value's raw bytes verbatim). They must not be decoded as a
	// plain string / []byte, so flag them before falling through to the underlying type.
	if named, ok := t.(*types.Named); ok {
		if obj := named.Obj(); obj.Pkg() != nil {
			switch obj.Pkg().Path() + "." + obj.Name() {
			case "encoding/json.Number":
				return `{"k":"number"}`
			case "encoding/json.RawMessage":
				return `{"k":"raw"}`
			case "time.Time":
				// time.Time decodes from its RFC3339 string via Time.UnmarshalJSON.
				return `{"k":"time"}`
			}
		}
	}
	// A user type with its own json.Unmarshaler (UnmarshalJSON) or, failing that,
	// encoding.TextUnmarshaler (UnmarshalText) decodes via that method. Emit a marker plus
	// the type's runtime id (and CLR name for a struct, so the decoder can build a settable
	// receiver instance) — checked before the structural cases so it wins over the
	// underlying representation. Only honored when the method is a lowered Go body (so a
	// callback-bridge adapter exists); stdlib shim types like time.Time are excluded above.
	if kind, id, clr, ok := l.jsonUserUnmarshaler(t); ok {
		s := `{"k":"` + kind + `","id":` + strconv.FormatInt(id, 10)
		if clr != "" {
			s += `,"n":` + strconv.Quote(clr)
		}
		return s + `,"t":"` + typeDescStr(t) + `"}`
	}
	switch u := t.Underlying().(type) {
	case *types.Basic:
		// "t" carries the Go type's display name (int / int64 / main.Money) so a
		// json.Unmarshal type mismatch reports Go's exact "...Go value of type T" message.
		tn := `,"t":"` + typeDescStr(t) + `"}`
		switch {
		case u.Info()&types.IsBoolean != 0:
			return `{"k":"bool"` + tn
		case u.Info()&types.IsUnsigned != 0:
			return `{"k":"uint"` + tn
		case u.Info()&types.IsInteger != 0:
			return `{"k":"int"` + tn
		case u.Info()&types.IsFloat != 0:
			return `{"k":"float"` + tn
		case u.Info()&types.IsString != 0:
			return `{"k":"string"` + tn
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
			// An embedded (anonymous) struct field with no explicit json name has its
			// fields promoted into the parent object (Go's flattening). Mark it so the
			// decoder decodes the SAME object into the embedded value.
			if isEmbeddedStructField(f, u.Tag(i)) {
				b.WriteString(`{"embed":true,"c":`)
				b.WriteString(strconv.Quote(f.Name()))
				b.WriteString(`,"t":`)
				b.WriteString(l.jsonDescriptor(f.Type(), seen))
				b.WriteByte('}')
				continue
			}
			b.WriteString(`{"j":`)
			b.WriteString(strconv.Quote(key))
			b.WriteString(`,"c":`)
			b.WriteString(strconv.Quote(f.Name()))
			b.WriteString(`,"t":`)
			b.WriteString(l.jsonDescriptor(f.Type(), seen))
			if jsonStringOption(u.Tag(i)) {
				b.WriteString(`,"q":true`) // ,string: value is carried as a quoted JSON string
			}
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

// jsonUserUnmarshaler reports whether named type t decodes through its own
// UnmarshalJSON (json.Unmarshaler) or UnmarshalText (encoding.TextUnmarshaler) — both
// pointer-receiver, signature func([]byte) error — and returns the descriptor kind
// ("ujson"/"utext"), the type's runtime id (for the callback bridge), and the struct's
// CLR name (empty for a non-struct). Only true when the method has a lowered Go body, so
// a bridge adapter is generated; stdlib shim implementers (time.Time, big.Int) are not
// lowered and fall through to their own descriptor.
func (l *funcLowerer) jsonUserUnmarshaler(t types.Type) (kind string, id int64, clr string, ok bool) {
	named, isNamed := t.(*types.Named)
	if !isNamed {
		return "", 0, "", false
	}
	ptr := types.NewPointer(named)
	pkg := named.Obj().Pkg()
	for _, cand := range []struct{ name, k string }{{"UnmarshalJSON", "ujson"}, {"UnmarshalText", "utext"}} {
		obj, _, _ := types.LookupFieldOrMethod(ptr, true, pkg, cand.name)
		fn, isFn := obj.(*types.Func)
		if !isFn || l.byFunc[fn] == nil {
			continue
		}
		sig, sok := fn.Type().(*types.Signature)
		if !sok || sig.Params().Len() != 1 || sig.Results().Len() != 1 {
			continue
		}
		s, isSlice := sig.Params().At(0).Type().Underlying().(*types.Slice)
		if !isSlice {
			continue
		}
		if b, isBasic := s.Elem().Underlying().(*types.Basic); !isBasic || b.Kind() != types.Byte {
			continue
		}
		if _, isStruct := named.Underlying().(*types.Struct); isStruct {
			st := l.structFor(named)
			return cand.k, int64(st.Id), st.Name, true
		}
		nid, _ := l.namedIdentity(named)
		return cand.k, nid, "", true
	}
	return "", 0, "", false
}

// jsonFieldKey returns the JSON object key for a struct field and whether the
// field is skipped (`json:"-"`).
// isEmbeddedStructField reports whether f is an anonymous struct (or *struct) field
// with no explicit json name — Go promotes its fields into the parent JSON object.
func isEmbeddedStructField(f *types.Var, tag string) bool {
	if !f.Anonymous() {
		return false
	}
	if jt, ok := structTagLookup(tag, "json"); ok && strings.Split(jt, ",")[0] != "" {
		return false // an explicit json name (or "-") makes it a regular/skipped field
	}
	et := f.Type()
	if pt, ok := et.Underlying().(*types.Pointer); ok {
		et = pt.Elem()
	}
	_, isStruct := et.Underlying().(*types.Struct)
	return isStruct
}

// jsonStringOption reports whether a field's json tag carries the ",string" option
// (the value is encoded as / decoded from a quoted JSON string).
func jsonStringOption(tag string) bool {
	jt, ok := structTagLookup(tag, "json")
	if !ok {
		return false
	}
	parts := strings.Split(jt, ",")
	for _, p := range parts[1:] {
		if p == "string" {
			return true
		}
	}
	return false
}

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
