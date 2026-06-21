package lower

import (
	"go/types"

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
	id = int64(len(c.namedIds) + 1)
	c.namedIds[named] = id
	c.namedNames[id] = namedDisplayName(named)
	return id, true
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

// maybeWrapNamed wraps the boxed value on top of the stack with its named-type
// identity, if the static type t is an identity-bearing named type. Called right
// after a value is boxed into an interface slot.
func (l *funcLowerer) maybeWrapNamed(t types.Type) {
	id, ok := l.namedIdentity(t)
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
}
