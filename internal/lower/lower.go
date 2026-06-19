// Package lower turns a type-checked Go package into GoCLR IR.
//
// M1 lowers the structured subset directly from go/ast + go/types: functions,
// int/int32/bool/string locals, operators, if/for/switch with break/continue,
// range-over-string, function calls, println/print, and user-defined struct
// value types (composite literals and field access). Each Go variable becomes a
// CIL local. Anything outside the subset yields an actionable GCLR0301.
package lower

import (
	"go/ast"
	"go/token"
	"go/types"

	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
	"github.com/arturoeanton/go-netcore/internal/goir"
)

// lowerCtx is state shared across all functions of a package: type resolution
// (including the struct registry) and the function table for call resolution.
type lowerCtx struct {
	pkg         *frontend.Package
	byName      map[string]*goir.Method   // free functions, by name
	byFunc      map[*types.Func]*goir.Method // methods, by *types.Func
	structReg   map[*types.Named]*goir.Struct
	anonStructReg map[string]*goir.Struct // anonymous structs, keyed by structural string
	structByName  map[string]*goir.Struct // dedup by emitted name (distinct *Named for one instantiation)
	structOrder []*goir.Struct
	bag         *diagnostics.Bag
	prog        *goir.Program // for appending lifted closure methods
	closures    []*closureInfo
	invoke      *goir.Method // the generated function-value dispatcher
	needsInvoker bool        // a `go` statement or deferred thunk uses the dispatcher -> register it at startup
	// Generics: generic function templates (by name) are not shelled directly;
	// each concrete instantiation discovered at a call site is monomorphized into
	// its own method (monoInsts, keyed by name+type-args) and queued in monoTodo.
	genericDecls       map[string]*ast.FuncDecl
	genericMethodDecls map[*types.Func]*ast.FuncDecl // generic method templates, by origin *types.Func
	monoInsts          map[string]*goir.Method
	monoTodo           []monoJob
}

// monoJob is a queued generic-function instantiation whose body is lowered after
// the main pass, with subst mapping its type parameters to concrete types.
type monoJob struct {
	decl   *ast.FuncDecl
	method *goir.Method
	subst  map[*types.TypeParam]types.Type
}

// Lower lowers package main to a goir.Program.
func Lower(pkg *frontend.Package, bag *diagnostics.Bag) (*goir.Program, bool) {
	c := &lowerCtx{
		pkg:          pkg,
		byName:       map[string]*goir.Method{},
		byFunc:       map[*types.Func]*goir.Method{},
		structReg:     map[*types.Named]*goir.Struct{},
		anonStructReg: map[string]*goir.Struct{},
		structByName:  map[string]*goir.Struct{},
		genericDecls:       map[string]*ast.FuncDecl{},
		genericMethodDecls: map[*types.Func]*ast.FuncDecl{},
		monoInsts:          map[string]*goir.Method{},
		bag:                bag,
	}

	var decls []*ast.FuncDecl
	for _, f := range pkg.Syntax {
		for _, d := range f.Decls {
			if fd, ok := d.(*ast.FuncDecl); ok {
				decls = append(decls, fd)
			}
		}
	}

	prog := &goir.Program{}
	c.prog = prog

	// First pass: method shells (signatures) so calls can resolve forward refs.
	type pending struct {
		decl   *ast.FuncDecl
		method *goir.Method
	}
	var todo []pending
	for _, fd := range decls {
		// Generic functions are templates: skip the shell and instantiate them
		// per concrete type-argument set as call sites are discovered.
		if fd.Recv == nil && fd.Type.TypeParams != nil && fd.Type.TypeParams.NumFields() > 0 {
			c.genericDecls[fd.Name.Name] = fd
			continue
		}
		// Generic methods (receiver has type parameters, e.g. (*Stack[T]).Push) are
		// likewise templates, instantiated per receiver type-argument set.
		if fd.Recv != nil {
			if fn, ok := c.pkg.TypesInfo.Defs[fd.Name].(*types.Func); ok {
				if sig, ok := fn.Type().(*types.Signature); ok && sig.RecvTypeParams().Len() > 0 {
					c.genericMethodDecls[fn] = fd
					continue
				}
			}
		}
		m, ok := c.methodShell(fd)
		if !ok {
			return nil, false
		}
		prog.Methods = append(prog.Methods, m)
		if fd.Recv == nil {
			c.byName[fd.Name.Name] = m
			if fd.Name.Name == "main" {
				prog.Entry = m
			}
		} else if fn, ok := c.pkg.TypesInfo.Defs[fd.Name].(*types.Func); ok {
			c.byFunc[fn] = m
		}
		todo = append(todo, pending{fd, m})
	}
	if prog.Entry == nil {
		bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeNoMainPackage,
			"no func main in package main").WithPackage(pkg.PkgPath))
		return nil, false
	}

	// Second pass: bodies.
	for _, p := range todo {
		l := &funcLowerer{lowerCtx: c, m: p.method, ok: true}
		l.build(p.decl)
		if !l.ok {
			return nil, false
		}
	}

	// Drain the monomorphization worklist: each generic instantiation's body is
	// lowered with its type-parameter substitution. Lowering one may discover
	// further instantiations, so loop until the worklist is empty.
	for len(c.monoTodo) > 0 {
		job := c.monoTodo[0]
		c.monoTodo = c.monoTodo[1:]
		l := &funcLowerer{lowerCtx: c, m: job.method, ok: true, typeSubst: job.subst}
		l.build(job.decl)
		if !l.ok {
			return nil, false
		}
	}

	c.finishInvoke()
	// If goroutines or deferred thunks use the closure dispatcher, register it with
	// the runtime at the very start of main (GoRuntime.Go / GoDefers.Run need it).
	if c.needsInvoker && prog.Entry != nil {
		prog.Entry.Code = append([]goir.Op{{Code: goir.OpRegisterInvoker}}, prog.Entry.Code...)
	}
	prog.Structs = c.structOrder
	return prog, true
}

// methodShell builds a method signature from a func decl. A method becomes a
// static method named TypeName_Method with the receiver as its first parameter.
func (c *lowerCtx) methodShell(fd *ast.FuncDecl) (*goir.Method, bool) {
	m := &goir.Method{Name: fd.Name.Name, GoName: fd.Name.Name, Ret: goir.TVoid}

	if fd.Recv != nil {
		rt, ok := c.goType(c.pkg.TypesInfo.TypeOf(fd.Recv.List[0].Type))
		if !ok {
			return nil, c.unsupported(fd.Recv.Pos(), "receiver type")
		}
		m.Params = append(m.Params, rt)
		m.Name = c.recvTypeName(fd) + "_" + fd.Name.Name
		m.GoName = m.Name
	}

	if fd.Type.Params != nil {
		for _, field := range fd.Type.Params.List {
			t, ok := c.fieldParamType(field)
			if !ok {
				return nil, c.unsupported(field.Type.Pos(), "parameter type")
			}
			n := len(field.Names)
			if n == 0 {
				n = 1
			}
			for i := 0; i < n; i++ {
				m.Params = append(m.Params, t)
			}
		}
	}
	if fd.Type.Results != nil {
		for _, field := range fd.Type.Results.List {
			t, ok := c.goType(c.pkg.TypesInfo.TypeOf(field.Type))
			if !ok {
				return nil, c.unsupported(field.Type.Pos(), "result type")
			}
			n := len(field.Names)
			if n == 0 {
				n = 1
			}
			for i := 0; i < n; i++ {
				m.Results = append(m.Results, t)
			}
		}
	}
	switch len(m.Results) {
	case 0:
		m.Ret = goir.TVoid
	case 1:
		m.Ret = m.Results[0]
	default:
		m.Ret = goir.TObjectArray // a boxed tuple
	}
	return m, true
}

// fieldParamType resolves a parameter field's type, mapping a variadic `...T`
// parameter to its in-function slice type []T.
func (c *lowerCtx) fieldParamType(field *ast.Field) (goir.Type, bool) {
	if ell, ok := field.Type.(*ast.Ellipsis); ok {
		et, ok := c.goType(c.pkg.TypesInfo.TypeOf(ell.Elt))
		if !ok {
			return goir.Type{}, false
		}
		return goir.SliceType(et), true
	}
	return c.goType(c.pkg.TypesInfo.TypeOf(field.Type))
}

// recvTypeName returns the base named type of a method receiver.
func (c *lowerCtx) recvTypeName(fd *ast.FuncDecl) string {
	t := c.pkg.TypesInfo.TypeOf(fd.Recv.List[0].Type)
	if p, ok := t.(*types.Pointer); ok {
		t = p.Elem()
	}
	if named, ok := t.(*types.Named); ok {
		return named.Obj().Name()
	}
	return "T"
}

// goType maps a go/types.Type to a goir.Type, registering struct types on demand.
func (c *lowerCtx) goType(t types.Type) (goir.Type, bool) {
	if named, ok := t.(*types.Named); ok {
		if _, isStruct := named.Underlying().(*types.Struct); isStruct {
			return goir.StructType(c.structFor(named)), true
		}
	}
	// Anonymous struct types (e.g. `struct{ x int }`) — register structurally so
	// identical anonymous structs share one TypeDef (value semantics, comparison).
	if st, ok := t.(*types.Struct); ok {
		return goir.StructType(c.structForAnon(st)), true
	}
	if sl, ok := t.Underlying().(*types.Slice); ok {
		et, ok := c.goType(sl.Elem())
		if !ok {
			return goir.Type{}, false
		}
		return goir.SliceType(et), true
	}
	if mp, ok := t.Underlying().(*types.Map); ok {
		kt, ok1 := c.goType(mp.Key())
		vt, ok2 := c.goType(mp.Elem())
		if !ok1 || !ok2 {
			return goir.Type{}, false
		}
		return goir.MapType(kt, vt), true
	}
	if pt, ok := t.Underlying().(*types.Pointer); ok {
		et, ok := c.goType(pt.Elem())
		if !ok {
			return goir.Type{}, false
		}
		return goir.PtrType(et), true
	}
	if _, ok := t.Underlying().(*types.Interface); ok {
		// Both empty (any) and named interfaces (error, Stringer, …) are opaque
		// objects; method dispatch is generated at the call site via isinst.
		return goir.TObject, true
	}
	if _, ok := t.Underlying().(*types.Signature); ok {
		return goir.TFunc, true // a function value -> GoClosure
	}
	if ch, ok := t.Underlying().(*types.Chan); ok {
		et, ok := c.goType(ch.Elem())
		if !ok {
			return goir.Type{}, false
		}
		return goir.ChanType(et), true
	}
	b, ok := t.Underlying().(*types.Basic)
	if !ok {
		return goir.Type{}, false
	}
	switch b.Kind() {
	case types.Int, types.Int64, types.UntypedInt:
		return goir.TInt64, true
	case types.Int32, types.UntypedRune:
		return goir.TInt32, true
	case types.Uint8: // byte: indexing a string yields this
		return goir.TInt32, true
	case types.Uint, types.Uint64, types.Uintptr:
		return goir.TUint64, true
	case types.Uint32, types.Uint16:
		return goir.TUint32, true
	case types.Float64, types.UntypedFloat:
		return goir.TFloat64, true
	case types.Float32:
		return goir.TFloat32, true
	case types.Complex128, types.UntypedComplex, types.Complex64:
		return goir.TComplex, true
	case types.Bool, types.UntypedBool:
		return goir.TBool, true
	case types.String, types.UntypedString:
		return goir.TString, true
	default:
		return goir.Type{}, false
	}
}

// structFor returns (registering if needed) the IR descriptor for a named struct.
func (c *lowerCtx) structFor(named *types.Named) *goir.Struct {
	if s, ok := c.structReg[named]; ok {
		return s
	}
	st := named.Underlying().(*types.Struct)
	name := named.Obj().Name()
	// Instantiated generic structs (List[int], List[string]) share a base name;
	// mangle the type arguments in so each instantiation is a distinct TypeDef.
	if ta := named.TypeArgs(); ta != nil && ta.Len() > 0 {
		name = mangleMono(name + "[" + typeListString(ta) + "]")
	}
	// Distinct *types.Named can denote the same instantiation (e.g. Stack[int]
	// reached from a var decl vs. a method receiver); dedup by emitted name so
	// only one TypeDef is created and casts between them succeed.
	if s, ok := c.structByName[name]; ok {
		c.structReg[named] = s
		return s
	}
	s := &goir.Struct{Name: name, GoName: named.Obj().Name(), Id: len(c.structOrder) + 1}
	c.structReg[named] = s // register before fields to tolerate references
	c.structByName[name] = s
	c.structOrder = append(c.structOrder, s)
	for i := 0; i < st.NumFields(); i++ {
		f := st.Field(i)
		ft, ok := c.goType(f.Type())
		if !ok {
			c.unsupported(f.Pos(), "struct field type in "+named.Obj().Name())
			ft = goir.TVoid
		}
		s.Fields = append(s.Fields, goir.Field{Name: f.Name(), Type: ft})
	}
	return s
}

// structForAnon registers an anonymous struct type, keyed by its structural
// string so that identical anonymous structs map to one TypeDef.
func (c *lowerCtx) structForAnon(st *types.Struct) *goir.Struct {
	key := types.TypeString(st, nil)
	if s, ok := c.anonStructReg[key]; ok {
		return s
	}
	id := len(c.structOrder) + 1
	s := &goir.Struct{Name: "__anon" + itoa(id), GoName: key, Id: id}
	c.anonStructReg[key] = s
	c.structOrder = append(c.structOrder, s)
	for i := 0; i < st.NumFields(); i++ {
		f := st.Field(i)
		ft, ok := c.goType(f.Type())
		if !ok {
			c.unsupported(f.Pos(), "anonymous struct field type")
			ft = goir.TVoid
		}
		s.Fields = append(s.Fields, goir.Field{Name: f.Name(), Type: ft})
	}
	return s
}

func (c *lowerCtx) unsupported(pos token.Pos, what string) bool {
	var dp diagnostics.Position
	if c.pkg.Fset != nil && pos != token.NoPos {
		p := c.pkg.Fset.Position(pos)
		dp = diagnostics.Position{File: p.Filename, Line: p.Line, Col: p.Column}
	}
	c.bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeUnsupportedFeature,
		"unsupported in the M1 subset: "+what).
		WithPackage(c.pkg.PkgPath).
		WithPos(dp).
		WithReason("the goclr backend (milestone M1) lowers functions, int/int32/bool/string locals, operators, if/for/switch, range-over-string, calls, structs, and println/print.").
		WithSuggestion("see ROADMAP.md — pointers, slices, maps, and interfaces arrive in later increments."))
	return false
}

// funcLowerer lowers a single function body.
type funcLowerer struct {
	*lowerCtx
	m         *goir.Method
	ok        bool
	locals    map[types.Object]int
	nextLbl   int
	breaks    []int
	continues []int
	// goto/label support: gotoLabels maps a Go label name to its IR label id,
	// allocated lazily so forward gotos resolve. labeledBreaks/labeledContinues
	// map a loop label name to that loop's end/post IR labels so a labeled
	// break/continue can target an enclosing labeled loop. pendingLoopLabel, when
	// set by labeledStmt, carries a labeled loop's pre-allocated break/continue
	// labels into the loop lowering that immediately follows.
	gotoLabels       map[string]int
	labeledBreaks    map[string]int
	labeledContinues map[string]int
	pendingLoopLabel *loopLabels
	// addrTaken holds locals whose address is taken; they are stored as GoPtr
	// cells. cells maps such a local's index to its pointee (logical) type.
	addrTaken   map[types.Object]bool
	cells       map[int]goir.Type
	resultTypes []goir.Type // for multi-return packing
	// defer support: when the function uses defer, the body runs inside a
	// try/catch and returns redirect to deferNormalLabel.
	deferMode        bool
	deferNormalLabel int
	resultLocal      int
	// closure lowering: the body of a lifted function literal.
	inClosure  bool
	closureRet goir.Type
	// namedResults holds the local indices of named return values, in order, when
	// the function declares them (e.g. func f() (err error)); empty otherwise.
	namedResults []int
	// typeSubst, when set, maps the type parameters of a generic function being
	// monomorphized to concrete types; goType applies it before resolving.
	typeSubst map[*types.TypeParam]types.Type
}

func (l *funcLowerer) build(fd *ast.FuncDecl) {
	l.locals = map[types.Object]int{}
	l.cells = map[int]goir.Type{}
	l.gotoLabels = map[string]int{}
	l.labeledBreaks = map[string]int{}
	l.labeledContinues = map[string]int{}
	l.addrTaken = l.analyzeAddrTaken(fd.Body)
	l.resultTypes = l.m.Results

	// Prologue: copy the receiver (if any) and each parameter into a local so
	// access is uniform. An address-taken local becomes a GoPtr cell.
	arg := 0
	if fd.Recv != nil {
		rf := fd.Recv.List[0]
		t, _ := l.goType(l.pkg.TypesInfo.TypeOf(rf.Type))
		if len(rf.Names) == 1 && rf.Names[0].Name != "_" {
			obj := l.pkg.TypesInfo.Defs[rf.Names[0]]
			idx, _ := l.declareLocal(obj, t)
			a := arg
			l.initLocal(idx, func() { l.emit(goir.Op{Code: goir.OpLdArg, Arg: a}) })
		}
		arg++
	}
	if fd.Type.Params != nil {
		for _, field := range fd.Type.Params.List {
			t, _ := l.fieldParamType(field)
			for _, name := range field.Names {
				obj := l.pkg.TypesInfo.Defs[name]
				idx, _ := l.declareLocal(obj, t)
				a := arg
				l.initLocal(idx, func() { l.emit(goir.Op{Code: goir.OpLdArg, Arg: a}) })
				arg++
			}
		}
	}

	// Named return values become zero-initialized locals (cells if captured by a
	// deferred closure), so `return`/naked returns and deferred mutations of them
	// (the recover-to-named-error idiom) all read/write one slot.
	if fd.Type.Results != nil {
		for _, field := range fd.Type.Results.List {
			if len(field.Names) == 0 {
				continue
			}
			rt, _ := l.goType(l.pkg.TypesInfo.TypeOf(field.Type))
			for _, name := range field.Names {
				if name.Name == "_" {
					l.namedResults = append(l.namedResults, -1)
					continue
				}
				obj := l.pkg.TypesInfo.Defs[name]
				idx, _ := l.declareLocal(obj, rt)
				rtCopy := rt
				l.initLocal(idx, func() { l.emitZeroValue(rtCopy) })
				l.namedResults = append(l.namedResults, idx)
			}
		}
	}

	l.deferMode = hasDefer(fd)
	if l.deferMode {
		l.buildDeferredBody(fd)
		return
	}
	if fd.Body != nil {
		l.block(fd.Body)
	}
	l.emit(goir.Op{Code: goir.OpRet})
}

func (l *funcLowerer) emit(op goir.Op) { l.m.Code = append(l.m.Code, op) }

func (l *funcLowerer) label() int { l.nextLbl++; return l.nextLbl }

func (l *funcLowerer) mark(id int) { l.emit(goir.Op{Code: goir.OpLabel, Label: id}) }

func (l *funcLowerer) addLocal(obj types.Object, t goir.Type) int {
	idx := len(l.m.Locals)
	l.m.Locals = append(l.m.Locals, t)
	if obj != nil {
		l.locals[obj] = idx
	}
	return idx
}

func (l *funcLowerer) fail(pos token.Pos, what string) {
	l.ok = false
	l.unsupported(pos, what)
}
