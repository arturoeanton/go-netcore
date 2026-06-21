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

// buildHandlerClosure emits a lifted method adapting args[0]=receiver (a GoPtr),
// args[1]=ResponseWriter, args[2]=*Request to ServeHTTP's parameters, then calls it.
func (c *lowerCtx) buildHandlerClosure(m *goir.Method, src recvSource) int {
	id := len(c.closures)
	method := &goir.Method{
		Name:    fmt.Sprintf("__handler_%d", id),
		GoName:  fmt.Sprintf("__handler_%d", id),
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
	}
	// ResponseWriter from args[1], *Request from args[2].
	for i := 1; i <= 2; i++ {
		cl.emit(goir.Op{Code: goir.OpLdArg, Arg: 1})
		cl.emit(goir.Op{Code: goir.OpLdcI4, Int: int64(i)})
		cl.emit(goir.Op{Code: goir.OpLdElemRef})
		cl.emitUnbox(m.Params[i])
	}
	cl.emit(goir.Op{Code: goir.OpCallMethod, Callee: m})
	cl.emit(goir.Op{Code: goir.OpLdNull})
	cl.emit(goir.Op{Code: goir.OpRet})
	return id
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
