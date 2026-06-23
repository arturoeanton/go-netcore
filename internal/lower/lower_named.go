package lower

import (
	"go/types"
	"sort"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// The typed box. A Go named type whose underlying type is not a struct boxes to
// the same CLR object as its underlying type, so its named-type identity is lost
// — breaking interface dispatch (which named slice satisfies sort.Interface?),
// fmt Stringer dispatch, and %T. When such a value is converted to an interface,
// lowering wraps it in a runtime GoNamed carrying a per-build type id; consumers
// (interface dispatch, type assert/switch, ==, fmt) recover the identity.
//
// Structs are excluded: their distinct CLR type already provides identity.

// namedIdentity returns the type id for an identity-bearing named type — one with
// a non-struct, non-interface underlying and a non-empty method set — assigning a
// fresh stable id on first sight. ok is false for types that don't need a tag.
func (c *lowerCtx) namedIdentity(t types.Type) (id int64, ok bool) {
	named, isNamed := t.(*types.Named)
	if !isNamed {
		return 0, false
	}
	switch named.Underlying().(type) {
	case *types.Struct, *types.Interface, nil:
		return 0, false // structs carry CLR identity; interfaces aren't concrete
	}
	// An opaque shim type (syscall.Signal -> GoSignal) already has a distinct CLR
	// class as its identity, so it must not also be wrapped in a typed box.
	if isOpaqueShimType(named) {
		return 0, false
	}
	// Only types observable by identity at runtime — those with a method set on T
	// or *T (Stringer/Error, sort.Interface implementers, enums with String, …).
	// Method-less named types keep their bare representation (a documented gap for
	// precise %T of such types; they cannot be dispatched or stringified anyway).
	if types.NewMethodSet(types.NewPointer(named)).Len() == 0 {
		return 0, false
	}
	if id, ok := c.namedIds[named]; ok {
		return id, true
	}
	id = c.nextTypeId() // shared with struct ids so the id is globally unique
	c.namedIds[named] = id
	c.namedNames[id] = namedDisplayName(named)
	return id, true
}

// nextTypeId returns the next id from the single counter shared by struct ids and
// typed-box named ids, so every runtime-dispatched type has a globally-unique id.
func (c *lowerCtx) nextTypeId() int64 {
	c.typeIdSeq++
	return c.typeIdSeq
}

// namedDisplayName renders a named type as Go's fmt does for %T: "pkg.Name"
// (e.g. "main.Money", "sort.StringSlice"). The builtin universe has no package.
func namedDisplayName(named *types.Named) string {
	obj := named.Obj()
	if obj.Pkg() == nil {
		return obj.Name()
	}
	return obj.Pkg().Name() + "." + obj.Name()
}

// wrapNamedExtern is the Rt.MakeNamed(value, id) call that boxes identity onto a
// value already boxed on the stack.
func (l *funcLowerer) wrapNamedExtern() *goir.Extern {
	return &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "MakeNamed",
		Params: []goir.Type{goir.TObject, goir.TInt64}, Ret: goir.TObject,
	}
}

// typeTagFor extends namedIdentity to also cover composite types — slices, maps,
// arrays — whose precise Go type (element/key types) the runtime value erases. A
// bare GoSlice/GoMap reports "[]interface {}" / "map[string]interface {}" for %T, so
// a value of static type []int or map[string]int is wrapped in a typed box carrying
// an id that maps to its Go type string ("[]int"). Returns ok=false when t needs no
// tag: a struct (its CLR class is identity), an interface (the value carries its own
// dynamic tag), or a composite whose element/key is itself an interface (the
// "[]interface {}" fallback is already correct, so the common []any path stays bare).
func (c *lowerCtx) typeTagFor(t types.Type) (int64, bool) {
	if id, ok := c.namedIdentity(t); ok {
		return id, true
	}
	switch t.Underlying().(type) {
	case *types.Slice, *types.Map, *types.Array:
		if compositeHasInterfaceElem(t) {
			return 0, false
		}
		return c.tagComposite(t), true
	case *types.Basic:
		// The html/template safe-string types (template.HTML/URL/JS/CSS/...) bypass the
		// contextual auto-escaper, so they must carry their identity through an interface
		// (Execute's data, a map[string]any field) — they are method-less named strings
		// that would otherwise be indistinguishable from untrusted input.
		if isHTMLSafeType(t) {
			return c.tagComposite(t), true
		}
	}
	return 0, false
}

// tagComposite assigns (and registers the %T name of) a stable id for a non-struct,
// non-interface type whose runtime value erases its precise Go type.
func (c *lowerCtx) tagComposite(t types.Type) int64 {
	key := types.TypeString(t, nil)
	if id, ok := c.compositeIds[key]; ok {
		return id
	}
	id := c.nextTypeId()
	c.compositeIds[key] = id
	c.namedNames[id] = typeDescStr(t) // Go's %T spelling: "[]int", "template.HTML"
	// If the element/value type carries runtime identity (a Stringer enum, an error,
	// …), record its id so fmt can re-tag the bare elements of this composite and
	// dispatch their String()/Error() — registering the element in namedIds also makes
	// collectStringers emit its stringer even when only the composite is ever boxed.
	if elem := compositeElemType(t); elem != nil {
		// typeTagFor (not just namedIdentity) so a nested composite element — a [][]Color's
		// inner []Color — is itself tagged, building the chain fmt re-tags down at runtime.
		if eid, ok := c.typeTagFor(elem); ok {
			c.compositeElem[id] = eid
		}
	}
	return id
}

// compositeElemType returns the element type of a slice/array, or the value type of a
// map — the type whose values are stored as the composite's elements.
func compositeElemType(t types.Type) types.Type {
	switch u := t.Underlying().(type) {
	case *types.Slice:
		return u.Elem()
	case *types.Array:
		return u.Elem()
	case *types.Map:
		return u.Elem()
	}
	return nil
}

// safeField is a struct field whose static type is an html/template trusted-string
// type — registered at startup so the template engine can bypass escaping for it even
// when read by reflection (the field's runtime value is an indistinguishable string).
type safeField struct{ structName, fieldName, kind string }

// safeFields scans every lowered struct for fields of an html/template safe type.
func (c *lowerCtx) safeFields() []safeField {
	var out []safeField
	for named, s := range c.structReg {
		st, ok := named.Underlying().(*types.Struct)
		if !ok {
			continue
		}
		for i := 0; i < st.NumFields(); i++ {
			f := st.Field(i)
			if f.Exported() && isHTMLSafeType(f.Type()) {
				out = append(out, safeField{s.Name, f.Name(), typeDescStr(f.Type())})
			}
		}
	}
	sort.Slice(out, func(i, j int) bool {
		if out[i].structName != out[j].structName {
			return out[i].structName < out[j].structName
		}
		return out[i].fieldName < out[j].fieldName
	})
	return out
}

// isHTMLSafeType reports whether t is one of html/template's trusted string types.
func isHTMLSafeType(t types.Type) bool {
	named, ok := t.(*types.Named)
	if !ok || named.Obj().Pkg() == nil || named.Obj().Pkg().Path() != "html/template" {
		return false
	}
	switch named.Obj().Name() {
	case "HTML", "HTMLAttr", "JS", "JSStr", "CSS", "URL", "Srcset":
		return true
	}
	return false
}

// compositeHasInterfaceElem reports whether t is a slice/array/map whose element
// (or, for a map, key or value) type is an interface — the case whose erased
// runtime representation already matches Go's %T, so no typed box is needed.
func compositeHasInterfaceElem(t types.Type) bool {
	isIface := func(e types.Type) bool { _, ok := e.Underlying().(*types.Interface); return ok }
	switch u := t.Underlying().(type) {
	case *types.Slice:
		return isIface(u.Elem())
	case *types.Array:
		return isIface(u.Elem())
	case *types.Map:
		return isIface(u.Key()) || isIface(u.Elem())
	}
	return false
}

// maybeWrapNamed wraps the boxed value on top of the stack with its type identity,
// if the static type t is an identity-bearing named type or a composite whose
// element types %T would otherwise erase. Called right after a value is boxed into
// an interface slot.
func (l *funcLowerer) maybeWrapNamed(t types.Type) {
	// A pointer to an identity-bearing pointee (*Color, *[]int): stamp the boxed GoPtr with
	// the pointee's id so %T reports *main.Color / *[]int instead of erasing to the bare
	// pointee value's type. The pointer stays a GoPtr (no GoNamed wrapper, which would break
	// deref/dispatch), so this uses TagPtr, not the wrap extern.
	if pt, ok := t.Underlying().(*types.Pointer); ok {
		if id, ok := l.typeTagFor(pt.Elem()); ok {
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "TagPtr",
				Params: []goir.Type{goir.TObject, goir.TInt64}, Ret: goir.TObject,
			}})
		}
		return
	}
	id, ok := l.typeTagFor(t)
	if !ok {
		return
	}
	l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.wrapNamedExtern()})
}

// unwrapNamedExtern is the Rt.Unwrap(obj) call that strips a GoNamed wrapper back
// to the underlying boxed value (pass-through if not wrapped).
func (l *funcLowerer) unwrapNamedExtern() *goir.Extern {
	return &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "Unwrap",
		Params: []goir.Type{goir.TObject}, Ret: goir.TObject,
	}
}

// emitUnwrapNamed strips a possible GoNamed wrapper from the object on top of the
// stack, leaving the underlying boxed value.
func (l *funcLowerer) emitUnwrapNamed() {
	l.emit(goir.Op{Code: goir.OpCallExtern, Extern: l.unwrapNamedExtern()})
}

// namedIdExtern is the Rt.NamedId(obj) call returning a boxed value's named-type
// id (0 if it carries no identity). Used at interface dispatch to match typed-box
// value implementers.
func (l *funcLowerer) namedIdExtern() *goir.Extern {
	return &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "NamedId",
		Params: []goir.Type{goir.TObject}, Ret: goir.TInt64,
	}
}

// emitRegisterNamedTypes appends, to the given lowerer, the startup calls that
// record each identity-bearing named type's display name with the runtime (for
// %T and reflect).
func (l *funcLowerer) emitRegisterNamedTypes() {
	for id, name := range l.namedNames {
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: name})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "RegisterNamedType",
			Params: []goir.Type{goir.TInt64, goir.TString}, Ret: goir.TVoid,
		}})
	}
	// Composite-element identities, so fmt can re-tag a []Stringer's bare elements.
	for compID, elemID := range l.compositeElem {
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: compID})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: elemID})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Rt", Method: "RegisterCompositeElem",
			Params: []goir.Type{goir.TInt64, goir.TInt64}, Ret: goir.TVoid,
		}})
	}
}
