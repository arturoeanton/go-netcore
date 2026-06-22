package lower

import (
	"fmt"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// handlerReg records a generated ServeHTTP dispatch closure to register with the
// net/http runtime at startup, keyed by the implementing type's runtime type id
// (the GoPtr/GoNamed tag the server loop sees when it holds an http.Handler).
type handlerReg struct {
	id        int64
	closureID int
}

// bridgeReg records a generated method-callback adapter to register with the runtime at
// startup, keyed by the implementing type's runtime id + the Go method name, so a shim
// can drive that method via GoRuntime.CallMethod.
type bridgeReg struct {
	id        int64
	method    string
	closureID int
}

// collectHandlers finds every struct type implementing http.Handler (a ServeHTTP
// method) and generates a closure the net/http server shim can invoke to drive the
// handler. This bridges goclr's static interface dispatch across the shim boundary:
// the HttpListener loop holds an opaque handler value and needs to call ServeHTTP on
// whatever concrete type the program supplied (e.g. gin's *Engine, echo's *Echo).
func (c *lowerCtx) collectHandlers() {
	for named, st := range c.structReg {
		fn, ptrRecv, ok := serveHTTPMethod(named)
		if !ok {
			continue
		}
		m := c.byFunc[fn]
		if m == nil || len(m.Params) != 3 {
			continue
		}
		c.needsInvoker = true
		c.invokeMethod()
		src := ptrDirect
		if !ptrRecv {
			src = ptrDeref // value receiver reached through the *T the server holds
		}
		c.handlers = append(c.handlers, handlerReg{id: int64(st.Id), closureID: c.buildHandlerClosure(m, src)})
	}
}

// serveHTTPMethod returns a type's ServeHTTP method (the http.Handler contract:
// two params, no results), and whether its receiver is a pointer.
func serveHTTPMethod(named *types.Named) (fn *types.Func, ptrRecv, ok bool) {
	obj, _, _ := types.LookupFieldOrMethod(named, true, named.Obj().Pkg(), "ServeHTTP")
	if obj == nil {
		obj, _, _ = types.LookupFieldOrMethod(types.NewPointer(named), true, named.Obj().Pkg(), "ServeHTTP")
	}
	f, isFn := obj.(*types.Func)
	if !isFn {
		return nil, false, false
	}
	sig, isSig := f.Type().(*types.Signature)
	if !isSig || sig.Params().Len() != 2 || sig.Results().Len() != 0 {
		return nil, false, false
	}
	return f, isPointerType(sig.Recv().Type()), true
}

// buildHandlerClosure adapts the http.Handler.ServeHTTP contract (no result) — a thin
// wrapper over the general buildMethodAdapter.
func (c *lowerCtx) buildHandlerClosure(m *goir.Method, src recvSource) int {
	return c.buildMethodAdapter(m, src)
}

// buildMethodAdapter emits a receiver-first dispatch closure for ANY method: it unpacks
// args[0]=receiver (adapted per src — a GoPtr used directly for a pointer receiver, or
// dereferenced for a value receiver) and args[1..n]=the method's params, calls the
// lowered method, and returns its (boxed) result or null for a void method. This is the
// adapter both the net/http handler bridge and the general interface-callback bridge
// (container/heap, …) register so a shim can drive the method through GoRuntime.CallMethod.
func (c *lowerCtx) buildMethodAdapter(m *goir.Method, src recvSource) int {
	id := len(c.closures)
	method := &goir.Method{
		Name:    fmt.Sprintf("__adapter_%d", id),
		GoName:  fmt.Sprintf("__adapter_%d", id),
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

	// Receiver from args[0].
	cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
	cl.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
	cl.emit(goir.Op{Code: goir.OpLdElemRef})
	switch src {
	case ptrDirect:
		cl.emitUnbox(recvType) // recvType is *T
	case ptrDeref:
		cl.emitUnbox(goir.PtrType(recvType))
		cl.emit(goir.Op{Code: goir.OpPtrGet})
		cl.emitUnbox(recvType)
	case valBridge:
		// A value receiver reached through the bridge: the interface value may arrive as a
		// GoPtr (&v stored), a GoNamed (named non-struct value), or a bare struct value.
		// Bridge.RecvValue normalizes all three to the receiver payload, then unbox to T.
		cl.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Bridge", Method: "RecvValue",
			Params: []goir.Type{goir.TObject}, Ret: goir.TObject,
		}})
		cl.emitUnbox(recvType)
	}
	// Each declared param from args[i].
	for i := 1; i < len(m.Params); i++ {
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(m.Params[i])
	}
	cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	// Return the (boxed) result, or null for a void method.
	if m.Ret == goir.TVoid {
		cl.emit(goir.Op{Code: goir.OpLdNull})
	} else {
		cl.emitBox(m.Ret)
	}
	cl.emit(goir.Op{Code: goir.OpRet})
	return id
}

// bridgeInterfaces are the stdlib interfaces a C# shim calls back through via
// GoRuntime.CallMethod. For each one present in the import closure, collectBridgeMethods
// generates + registers an adapter per (implementing type, method). Keyed by "pkg.Type".
var bridgeInterfaces = []string{
	"container/heap.Interface",
	"io.Writer",  // so Fmt.WriteTo can drive a wrapper writer's own Write (echo.Response, …)
	"io/fs.FS",     // so fs.Stat can call fsys.Open
	"io/fs.File",   // so fs.Stat can call the opened file's Stat/Close
	"io/fs.StatFS", // so fs.Stat can take the StatFS fast path (fsys.Stat) like Go does
}

// collectBridgeMethods generates the method-callback adapters every concrete implementer
// of a bridgeInterface needs, registered at startup by the implementer's runtime type id.
func (c *lowerCtx) collectBridgeMethods() {
	for _, ifaceName := range bridgeInterfaces {
		named := c.lookupNamedType(ifaceName)
		if named == nil {
			continue // the program doesn't import this package
		}
		iface, ok := named.Underlying().(*types.Interface)
		if !ok {
			continue
		}
		for named, st := range c.structReg {
			if c.registerBridgeAdapters(named, st.Id, iface) {
				// Link the CLR struct name to its id so an implementer reached BY VALUE
				// (a value-receiver struct boxed as its CLR struct) resolves to the same
				// adapter id its pointer carries.
				if c.bridgeClrLinks == nil {
					c.bridgeClrLinks = map[string]int64{}
				}
				c.bridgeClrLinks[st.Name] = int64(st.Id)
			}
		}
		// Named non-struct implementers (a typed-box type like `type IntHeap []int`
		// reached as *IntHeap, or a named map reached by value): keyed by the same
		// unified id their GoPtr/GoNamed carries.
		for named, id := range c.namedIds {
			c.registerBridgeAdapters(named, int(id), iface)
		}
	}
}

// lookupNamedType resolves a "importpath.TypeName" to its *types.Named anywhere in the
// program's import closure (any named type, not only opaque shims), scanning once.
func (c *lowerCtx) lookupNamedType(name string) *types.Named {
	if c.namedByName == nil {
		c.namedByName = map[string]*types.Named{}
		seen := map[*types.Package]bool{}
		var visit func(p *types.Package)
		visit = func(p *types.Package) {
			if p == nil || seen[p] {
				return
			}
			seen[p] = true
			scope := p.Scope()
			for _, n := range scope.Names() {
				if tn, ok := scope.Lookup(n).(*types.TypeName); ok {
					if nm, ok := tn.Type().(*types.Named); ok {
						c.namedByName[p.Path()+"."+n] = nm
					}
				}
			}
			for _, imp := range p.Imports() {
				visit(imp)
			}
		}
		visit(c.root)
	}
	return c.namedByName[name]
}

// registerBridgeAdapters: if type `named` implements `iface`, generate an adapter for each
// interface method and record it under the type id the implementer's value carries (a *T
// GoPtr, a GoNamed for a named non-struct, or the struct value's CLR-linked id). Returns
// whether any adapter was registered.
func (c *lowerCtx) registerBridgeAdapters(named *types.Named, typeID int, iface *types.Interface) bool {
	ptr := types.NewPointer(named)
	if !types.Implements(ptr, iface) && !types.Implements(named, iface) {
		return false
	}
	registered := false
	for i := 0; i < iface.NumMethods(); i++ {
		im := iface.Method(i)
		obj, _, _ := types.LookupFieldOrMethod(ptr, true, im.Pkg(), im.Name())
		fn, isFn := obj.(*types.Func)
		if !isFn {
			continue
		}
		m := c.byFunc[fn]
		if m == nil {
			continue
		}
		c.needsInvoker = true
		c.invokeMethod()
		// A pointer-receiver method takes the *T (GoPtr) directly; a value-receiver method
		// goes through valBridge, which normalizes whatever form the value arrived in
		// (GoPtr when &v was stored, GoNamed for a named value, or a bare struct value).
		src := valBridge
		if isPointerType(fn.Type().(*types.Signature).Recv().Type()) {
			src = ptrDirect
		}
		c.bridges = append(c.bridges, bridgeReg{
			id:        int64(typeID),
			method:    im.Name(),
			closureID: c.buildMethodAdapter(m, src),
		})
		registered = true
	}
	return registered
}

// emitBridgeRegistrations appends the startup calls that register every collected
// method-callback adapter with the runtime, keyed by (implementing type id, method name).
func (l *funcLowerer) emitBridgeRegistrations() {
	// Link CLR struct names -> dispatch ids first, so a by-value struct receiver resolves.
	for clrName, id := range l.bridgeClrLinks {
		l.emit(goir.Op{Code: goir.OpStrConst, Str: clrName})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: id})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Bridge", Method: "LinkClrId",
			Params: []goir.Type{goir.TString, goir.TInt64}, Ret: goir.TVoid,
		}})
	}
	for _, br := range l.bridges {
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(br.closureID)})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		l.emit(goir.Op{Code: goir.OpNewObjArray})
		fnLoc := l.addLocal(nil, goir.TFunc)
		l.emit(goir.Op{Code: goir.OpClosNew})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: fnLoc})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: br.id})
		l.emit(goir.Op{Code: goir.OpStrConst, Str: br.method})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: fnLoc})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Bridge", Method: "RegisterMethod",
			Params: []goir.Type{goir.TInt64, goir.TString, goir.TFunc}, Ret: goir.TVoid,
		}})
	}
}

// emitHandlerRegistrations appends the startup calls that register every collected
// ServeHTTP closure with the net/http runtime, keyed by implementing type id.
func (l *funcLowerer) emitHandlerRegistrations() {
	for _, hr := range l.handlers {
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: int64(hr.closureID)})
		l.emit(goir.Op{Code: goir.OpLdcI4, Int: 0})
		l.emit(goir.Op{Code: goir.OpNewObjArray})
		fnLoc := l.addLocal(nil, goir.TFunc)
		l.emit(goir.Op{Code: goir.OpClosNew})
		l.emit(goir.Op{Code: goir.OpStLoc, Local: fnLoc})
		l.emit(goir.Op{Code: goir.OpLdcI8, Int: hr.id})
		l.emit(goir.Op{Code: goir.OpLdLoc, Local: fnLoc})
		l.emit(goir.Op{Code: goir.OpCallExtern, Extern: &goir.Extern{
			Assembly: shimAssembly, Namespace: shimAssembly, Type: "Http", Method: "RegisterHandler",
			Params: []goir.Type{goir.TInt64, goir.TFunc}, Ret: goir.TVoid,
		}})
	}
}
