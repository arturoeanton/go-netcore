package lower

import (
	"fmt"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// stringerReg records a generated String()/Error() dispatch closure to register
// with the fmt runtime at startup: keyed by the struct's CLR name (value receiver,
// reached when a value is formatted) or its runtime type id (pointer receiver, and
// value receivers reached through *T).
type stringerReg struct {
	byPtr     bool   // register under the pointee type id rather than the CLR name
	byNamedId bool   // register under a typed-box named id (named non-struct types)
	name      string // struct CLR name (byPtr == false && !byNamedId)
	id        int64  // struct type id (byPtr) or named-type id (byNamedId)
	closureID int
}

// recvSource selects how a generated stringer closure obtains its receiver from
// the boxed argument the fmt runtime passes it.
type recvSource int

const (
	valDirect   recvSource = iota // arg is a boxed struct value
	ptrDeref                      // arg is a GoPtr; dereference to the struct value
	ptrDirect                     // arg is a GoPtr; pass it as the pointer receiver
	namedUnwrap                   // arg is a GoNamed; unwrap, then unbox to the underlying value
)

// collectStringers finds every struct type with a String() or Error() method and
// generates closures the fmt runtime can invoke to format values of that type.
func (c *lowerCtx) collectStringers() {
	for named, st := range c.structReg {
		fn, ok := stringerMethod(named)
		if !ok {
			continue
		}
		m := c.byFunc[fn]
		if m == nil || len(m.Params) == 0 {
			continue
		}
		c.needsInvoker = true
		c.invokeMethod()
		if isPointerType(fn.Type().(*types.Signature).Recv().Type()) {
			// Pointer receiver: only *T carries the method.
			id := c.buildStringerClosure(m, ptrDirect)
			c.stringers = append(c.stringers, stringerReg{byPtr: true, id: int64(st.Id), closureID: id})
		} else {
			// Value receiver: a value is keyed by name; a *T derefs to the value.
			c.stringers = append(c.stringers,
				stringerReg{name: st.Name, closureID: c.buildStringerClosure(m, valDirect)},
				stringerReg{byPtr: true, id: int64(st.Id), closureID: c.buildStringerClosure(m, ptrDeref)})
		}
	}

	// Named non-struct types with a String()/Error() method (the typed box): a
	// value of such a type carries a GoNamed tag when boxed, so fmt dispatches by
	// type id. Only the identity-bearing named types that were actually boxed into
	// an interface (and thus appear in namedIds) can reach fmt this way.
	for named, id := range c.namedIds {
		fn, ok := stringerMethod(named)
		if !ok {
			continue
		}
		if isPointerType(fn.Type().(*types.Signature).Recv().Type()) {
			continue // pointer-receiver stringer on a named non-struct: rare, deferred
		}
		m := c.byFunc[fn]
		if m == nil || len(m.Params) == 0 {
			continue
		}
		c.needsInvoker = true
		c.invokeMethod()
		c.stringers = append(c.stringers,
			stringerReg{byNamedId: true, id: id, closureID: c.buildStringerClosure(m, namedUnwrap)})
	}
}

// stringerMethod returns the String() or Error() method of a named type (value or
// pointer receiver, no params, one string result), preferring String.
func stringerMethod(named *types.Named) (*types.Func, bool) {
	for _, name := range []string{"String", "Error"} {
		obj, _, _ := types.LookupFieldOrMethod(named, true, named.Obj().Pkg(), name)
		if obj == nil {
			obj, _, _ = types.LookupFieldOrMethod(types.NewPointer(named), true, named.Obj().Pkg(), name)
		}
		fn, ok := obj.(*types.Func)
		if !ok {
			continue
		}
		sig, ok := fn.Type().(*types.Signature)
		if !ok || sig.Params().Len() != 0 || sig.Results().Len() != 1 {
			continue
		}
		if b, ok := sig.Results().At(0).Type().Underlying().(*types.Basic); ok && b.Kind() == types.String {
			return fn, true
		}
	}
	return nil, false
}

// buildStringerClosure emits a lifted method that adapts the boxed receiver from
// args[0] to m's receiver, calls m, and returns the boxed string. Returns its
// closure id.
func (c *lowerCtx) buildStringerClosure(m *goir.Method, src recvSource) int {
	id := len(c.closures)
	method := &goir.Method{
		Name:    fmt.Sprintf("__stringer_%d", id),
		GoName:  fmt.Sprintf("__stringer_%d", id),
		Params:  []goir.Type{goir.TObjectArray, goir.TObjectArray}, // env, args
		Ret:     goir.TObject,
		Results: []goir.Type{goir.TObject},
	}
	c.closures = append(c.closures, &closureInfo{id: id, method: method})
	c.prog.Methods = append(c.prog.Methods, method)

	cl := &funcLowerer{lowerCtx: c, m: method, ok: true}
	cl.locals = map[types.Object]int{}
	cl.cells = map[int]goir.Type{}
	recvType := m.Params[0]

	cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
	cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
	cl.emit(goir.Op{Code: goir.OpLdElemRef})
	switch src {
	case valDirect:
		cl.emitUnbox(recvType)
	case ptrDeref:
		cl.emitUnbox(goir.PtrType(recvType)) // castclass GoPtr
		cl.emit(goir.Op{Code: goir.OpPtrGet})
		cl.emitUnbox(recvType)
	case ptrDirect:
		cl.emitUnbox(recvType) // recvType is *T
	case namedUnwrap:
		cl.emitUnwrapNamed()   // GoNamed -> inner boxed underlying value
		cl.emitUnbox(recvType) // unbox to the underlying representation (e.g. i8)
	}
	cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	cl.emitBox(m.Ret)
	cl.emit(goir.Op{Code: goir.OpRet})
	return id
}

// emitStringerRegistrations appends, to the given lowerer, the startup calls that
// register every collected stringer closure with the fmt runtime.
func (l *funcLowerer) emitStringerRegistrations() {
	for _, sr := range l.stringers {
		// Build the GoClosure (empty env) for the lifted dispatch method.
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(sr.closureID)})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		l.emit(goir.Op{Code: goir.OpNewObjArray})
		if sr.byPtr || sr.byNamedId {
			// id-keyed registration: id is pushed before the closure. Pointer-receiver
			// struct stringers register by pointee type id; named non-struct stringers
			// register by typed-box named id.
			method := "RegisterPtrStringer"
			if sr.byNamedId {
				method = "RegisterNamedStringer"
			}
			idLoc := l.addLocal(nil, goir.TFunc)
			l.emit(goir.Op{Code: goir.OpClosNew})
			l.emit(goir.Op{Code: goir.OpStLoc, Local: idLoc})
			l.emit(goir.Op{Code: goir.OpLdcI8, Int: sr.id})
			l.emit(goir.Op{Code: goir.OpLdLoc, Local: idLoc})
			l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
				Assembly: shimAssembly, Namespace: shimAssembly, Type: "Fmt", Method: method,
				Params: []goir.Type{goir.TInt64, goir.TFunc}, Ret: goir.TVoid,
			}})
			continue
		}
		fnLoc := l.addLocal(nil, goir.TFunc)
		l.emit(goir.Op{Code: goir.OpClosNew})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: fnLoc})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: sr.name})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: fnLoc})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Fmt", Method: "RegisterValStringer",
			Params: []goir.Type{goir.TString, goir.TFunc}, Ret: goir.TVoid,
		}})
	}
}
