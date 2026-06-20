// Package lower turns a type-checked Go package into GoCLR IR.
//
// M1 lowers the structured subset directly from go/ast + go/types: functions,
// int/int32/bool/string locals, operators, if/for/switch with break/continue,
// range-over-string, function calls, println/print, and user-defined struct
// value types (composite literals and field access). Each Go variable becomes a
// CIL local. Anything outside the subset yields an actionable GCLR0301.
package lower

import (
	"fmt"
	"go/ast"
	"go/token"
	"go/types"
	"sort"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/diagnostics"
	"github.com/arturoeanton/go-netcore/internal/frontend"
	"github.com/arturoeanton/go-netcore/internal/goir"
)

// lowerCtx is state shared across all functions of a package: type resolution
// (including the struct registry) and the function table for call resolution.
type lowerCtx struct {
	pkg           *frontend.Package
	byName        map[string]*goir.Method      // free functions, by name
	byFunc        map[*types.Func]*goir.Method // methods, by *types.Func
	structReg     map[*types.Named]*goir.Struct
	anonStructReg map[string]*goir.Struct // anonymous structs, keyed by structural string
	structByName  map[string]*goir.Struct // dedup by emitted name (distinct *Named for one instantiation)
	structOrder   []*goir.Struct
	bag           *diagnostics.Bag
	prog          *goir.Program // for appending lifted closure methods
	closures      []*closureInfo
	invoke        *goir.Method // the generated function-value dispatcher
	needsInvoker  bool         // a `go` statement or deferred thunk uses the dispatcher -> register it at startup
	// Generics: generic function templates (by name) are not shelled directly;
	// each concrete instantiation discovered at a call site is monomorphized into
	// its own method (monoInsts, keyed by name+type-args) and queued in monoTodo.
	genericDecls       map[*types.Func]*ast.FuncDecl     // generic func templates, keyed by origin (bare name collides: cmp.Compare vs slices.Compare)
	genericDeclPkg     map[*types.Func]*frontend.Package // owning package of each generic func template (for cross-package instantiation)
	genericMethodDecls map[*types.Func]*ast.FuncDecl     // generic method templates, by origin *types.Func
	genericMethodPkg   map[*types.Func]*frontend.Package // owning package of each generic method template
	monoInsts          map[string]*goir.Method
	monoTodo           []monoJob
	// Multi-package: prefixOf maps each lowered package's *types.Package to the
	// CLR-name prefix that keeps cross-package symbols unique (empty for the root).
	prefixOf map[*types.Package]string
	// Package-level variables become static fields (globals); init runs their
	// initializers and the package init() functions before main.
	globals   map[*types.Var]int
	varInits  []varInit      // package-var initializers, in init order
	initFuncs []*goir.Method // package init() functions, in order
	// stringers holds generated String()/Error() dispatch closures to register
	// with the fmt runtime at startup.
	stringers []stringerReg
	// namedIds assigns each identity-bearing named type (non-struct underlying with
	// a method set) a stable per-build id so a boxed value can carry its Go named-
	// type identity — the typed box (see runtime GoNamed). namedNames maps id ->
	// display name ("pkg.Type") for %T / reflect, registered at startup.
	namedIds   map[*types.Named]int64
	namedNames map[int64]string
}

// varInit is a package-level variable initializer to run during program startup.
type varInit struct {
	pkg   *frontend.Package
	idx   int // global index
	gtype goir.Type
	value ast.Expr // nil => zero-initialize
}

// monoJob is a queued generic-function instantiation whose body is lowered after
// the main pass, with subst mapping its type parameters to concrete types. pkg is
// the package owning the template (for TypesInfo during body lowering).
type monoJob struct {
	decl   *ast.FuncDecl
	method *goir.Method
	subst  map[*types.TypeParam]types.Type
	pkg    *frontend.Package
}

// Lower lowers the main package and its non-stdlib dependency closure into one
// goir.Program (one CLR assembly). Symbols from non-root packages are CLR-name
// prefixed so they stay unique; cross-package calls resolve through the global
// byFunc table keyed by *types.Func.
func Lower(pkg *frontend.Package, bag *diagnostics.Bag) (*goir.Program, bool) {
	c := &lowerCtx{
		byName:             map[string]*goir.Method{},
		byFunc:             map[*types.Func]*goir.Method{},
		structReg:          map[*types.Named]*goir.Struct{},
		anonStructReg:      map[string]*goir.Struct{},
		structByName:       map[string]*goir.Struct{},
		genericDecls:       map[*types.Func]*ast.FuncDecl{},
		genericDeclPkg:     map[*types.Func]*frontend.Package{},
		genericMethodDecls: map[*types.Func]*ast.FuncDecl{},
		genericMethodPkg:   map[*types.Func]*frontend.Package{},
		monoInsts:          map[string]*goir.Method{},
		prefixOf:           map[*types.Package]string{},
		globals:            map[*types.Var]int{},
		namedIds:           map[*types.Named]int64{},
		namedNames:         map[int64]string{},
		bag:                bag,
	}
	prog := &goir.Program{}
	c.prog = prog

	// Collect the package set in dependency order (deps first, root last).
	pkgs := collectPackages(pkg)
	for _, p := range pkgs {
		prefix := ""
		if p != pkg {
			prefix = manglePkgPath(p.PkgPath) + "_"
		}
		if p.Types != nil {
			c.prefixOf[p.Types] = prefix
		}
	}

	// Global pass: register package-level variables as static fields (deps first).
	for _, p := range pkgs {
		c.pkg = p
		c.collectGlobals(p)
	}

	// Shell pass: every package's function/method signatures, so calls resolve.
	type pending struct {
		decl *ast.FuncDecl
		m    *goir.Method
		pkg  *frontend.Package
	}
	var todo []pending
	for _, p := range pkgs {
		c.pkg = p
		initN := 0
		for _, fd := range funcDecls(p) {
			if fd.Recv == nil && fd.Type.TypeParams != nil && fd.Type.TypeParams.NumFields() > 0 {
				if fn, ok := p.TypesInfo.Defs[fd.Name].(*types.Func); ok {
					c.genericDecls[fn] = fd
					c.genericDeclPkg[fn] = p
				}
				continue
			}
			if fd.Recv != nil {
				if fn, ok := p.TypesInfo.Defs[fd.Name].(*types.Func); ok {
					if sig, ok := fn.Type().(*types.Signature); ok && sig.RecvTypeParams().Len() > 0 {
						c.genericMethodDecls[fn] = fd
						c.genericMethodPkg[fn] = p
						continue
					}
				}
			}
			m, ok := c.methodShell(fd)
			if !ok {
				return nil, false
			}
			// init() functions: each gets a unique name and runs during startup,
			// not via the normal call path.
			if fd.Recv == nil && fd.Name.Name == "init" {
				m.Name = c.curPrefix() + "init__" + itoa(initN)
				m.GoName = m.Name
				initN++
				prog.Methods = append(prog.Methods, m)
				c.initFuncs = append(c.initFuncs, m)
				todo = append(todo, pending{fd, m, p})
				continue
			}
			prog.Methods = append(prog.Methods, m)
			if fn, ok := p.TypesInfo.Defs[fd.Name].(*types.Func); ok {
				c.byFunc[fn] = m
			}
			if fd.Recv == nil {
				c.byName[fd.Name.Name] = m
				if p == pkg && fd.Name.Name == "main" {
					prog.Entry = m
				}
			}
			todo = append(todo, pending{fd, m, p})
		}
	}
	if prog.Entry == nil {
		bag.Add(diagnostics.New(diagnostics.SeverityError, diagnostics.CodeNoMainPackage,
			"no func main in package main").WithPackage(pkg.PkgPath))
		return nil, false
	}

	// Body pass.
	for _, t := range todo {
		c.pkg = t.pkg
		l := &funcLowerer{lowerCtx: c, m: t.m, ok: true}
		l.build(t.decl)
		if !l.ok {
			return nil, false
		}
	}

	// Drain the monomorphization worklist (each job carries its owning package).
	for len(c.monoTodo) > 0 {
		job := c.monoTodo[0]
		c.monoTodo = c.monoTodo[1:]
		c.pkg = job.pkg
		l := &funcLowerer{lowerCtx: c, m: job.method, ok: true, typeSubst: job.subst}
		l.build(job.decl)
		if !l.ok {
			return nil, false
		}
	}

	// Generate String()/Error() dispatch closures for fmt (after all methods are
	// shelled, so byFunc is populated; before buildInit emits their registration).
	c.collectStringers()

	// Startup: run package-var initializers and init() functions before main.
	if init, ok := c.buildInit(); !ok {
		return nil, false
	} else if init != nil {
		prog.Methods = append(prog.Methods, init)
		prog.Entry.Code = append([]goir.Op{{Code: goir.OpCallMethod, Callee: init}}, prog.Entry.Code...)
	}

	c.finishInvoke()
	if c.needsInvoker && prog.Entry != nil {
		prog.Entry.Code = append([]goir.Op{{Code: goir.OpRegisterInvoker}}, prog.Entry.Code...)
	}
	prog.Structs = c.structOrder
	return prog, true
}

// collectGlobals registers a package's file-level variables as static-field
// globals and records their initializers (in source order) for startup.
func (c *lowerCtx) collectGlobals(p *frontend.Package) {
	prefix := c.curPrefix()
	for _, f := range p.Syntax {
		for _, d := range f.Decls {
			gd, ok := d.(*ast.GenDecl)
			if !ok || gd.Tok != token.VAR {
				continue
			}
			for _, spec := range gd.Specs {
				vs, ok := spec.(*ast.ValueSpec)
				if !ok {
					continue
				}
				for i, name := range vs.Names {
					if name.Name == "_" {
						continue
					}
					obj, ok := p.TypesInfo.Defs[name].(*types.Var)
					if !ok {
						continue
					}
					gt, ok := c.goType(obj.Type())
					if !ok {
						c.unsupported(name.Pos(), "global variable type "+name.Name)
						continue
					}
					idx := len(c.prog.Globals)
					c.prog.Globals = append(c.prog.Globals, &goir.Global{Name: prefix + name.Name, Type: gt})
					c.globals[obj] = idx
					var val ast.Expr
					if len(vs.Values) == len(vs.Names) {
						val = vs.Values[i]
					}
					c.varInits = append(c.varInits, varInit{pkg: p, idx: idx, gtype: gt, value: val})
				}
			}
		}
	}
}

// taggedStructs returns the registered structs that carry at least one field tag.
func (c *lowerCtx) taggedStructs() []*goir.Struct {
	var out []*goir.Struct
	for _, s := range c.structOrder {
		for _, f := range s.Fields {
			if f.Tag != "" {
				out = append(out, s)
				break
			}
		}
	}
	return out
}

// buildInit generates the startup method that registers struct tags (for
// reflect/json), runs package-var initializers (in dependency/source order) and
// then each init() function. Returns nil if there is nothing to do.
func (c *lowerCtx) buildInit() (*goir.Method, bool) {
	tagged := c.taggedStructs()
	if len(c.varInits) == 0 && len(c.initFuncs) == 0 && len(tagged) == 0 && len(c.stringers) == 0 && len(c.namedNames) == 0 {
		return nil, true
	}
	// The package-var initializers and tag registrations are emitted into a series
	// of bounded chunk methods (__goclr_init_N): a whole program's globals — e.g.
	// the unicode tables — would otherwise overflow the CLR's 64KB-per-method IL
	// limit. The top-level __goclr_init calls each chunk, then the init() funcs.
	const chunkOpBudget = 6000

	var chunks []*goir.Method
	newChunk := func() *funcLowerer {
		m := &goir.Method{
			Name:   fmt.Sprintf("__goclr_init_%d", len(chunks)),
			GoName: "__goclr_init",
			Ret:    goir.TVoid,
		}
		cl := &funcLowerer{lowerCtx: c, m: m, ok: true}
		cl.locals = map[types.Object]int{}
		cl.cells = map[int]goir.Type{}
		chunks = append(chunks, m)
		return cl
	}
	finishChunk := func(cl *funcLowerer) bool {
		cl.emit(goir.Op{Code: goir.OpRet})
		return cl.ok
	}

	cl := newChunk()
	// Register named-type display names (for %T / reflect) and String()/Error()
	// dispatch closures so fmt can format custom types.
	cl.emitRegisterNamedTypes()
	cl.emitStringerRegistrations()
	// Register struct field tags first so reflect/json see them everywhere.
	regExt := &goir.Extern{
		Assembly: shimAssembly, Namespace: shimAssembly, Type: "Reflect", Method: "RegisterTag",
		Params: []goir.Type{goir.TString, goir.TString, goir.TString}, Ret: goir.TVoid,
	}
	for _, s := range tagged {
		for _, f := range s.Fields {
			if f.Tag == "" {
				continue
			}
			cl.emit(goir.Op{Code: goir.OpStrConst, Str: s.Name})
			cl.emit(goir.Op{Code: goir.OpStrConst, Str: f.Name})
			cl.emit(goir.Op{Code: goir.OpStrConst, Str: f.Tag})
			cl.emit(goir.Op{Code: goir.OpCallExtern, Extern: regExt})
		}
	}
	for _, vi := range c.varInits {
		// Start a fresh chunk once the current one is sizeable, keeping each
		// var initializer atomic (it must not be split mid-expression).
		if len(cl.m.Code) > chunkOpBudget {
			if !finishChunk(cl) {
				return nil, false
			}
			cl = newChunk()
		}
		c.pkg = vi.pkg
		if vi.value != nil {
			cl.exprCoerced(vi.value, vi.gtype)
		} else {
			cl.emitZeroValue(vi.gtype)
		}
		cl.emit(goir.Op{Code: goir.OpStGlobal, Int: int64(vi.idx)})
	}
	if !finishChunk(cl) {
		return nil, false
	}
	c.prog.Methods = append(c.prog.Methods, chunks...)

	// Top-level init: call each chunk in order, then run init() functions.
	m := &goir.Method{Name: "__goclr_init", GoName: "__goclr_init", Ret: goir.TVoid}
	top := &funcLowerer{lowerCtx: c, m: m, ok: true}
	top.locals = map[types.Object]int{}
	top.cells = map[int]goir.Type{}
	for _, chunk := range chunks {
		top.emit(goir.Op{Code: goir.OpCallMethod, Callee: chunk})
	}
	for _, fn := range c.initFuncs {
		top.emit(goir.Op{Code: goir.OpCallMethod, Callee: fn})
	}
	top.emit(goir.Op{Code: goir.OpRet})
	return m, top.ok
}

// funcDecls returns the function declarations of a package.
func funcDecls(p *frontend.Package) []*ast.FuncDecl {
	var out []*ast.FuncDecl
	for _, f := range p.Syntax {
		for _, d := range f.Decls {
			if fd, ok := d.(*ast.FuncDecl); ok {
				out = append(out, fd)
			}
		}
	}
	return out
}

// compileFromSource lists pure, self-contained stdlib packages that goclr lowers
// from their real Go source instead of shimming — full-fidelity semantics with no
// native surface. They must import nothing outside this set and contain only code
// goclr can lower (structs, slices, maps, package-level table initializers).
var compileFromSource = map[string]bool{
	"unicode": true,
	"sort":    true, // via a goclr source overlay (drops internal/reflectlite)
	"cmp":     true, // tiny generic package (Less/Compare/Or over the Ordered set)
	"slices":  true, // generic slice helpers (depends only on cmp)
}

// collectPackages returns root plus its transitive non-stdlib dependencies that
// have Go source, in dependency order (a package appears after its imports).
// Stdlib packages in compileFromSource are included and lowered like any other.
func collectPackages(root *frontend.Package) []*frontend.Package {
	var order []*frontend.Package
	seen := map[*frontend.Package]bool{}
	var visit func(p *frontend.Package)
	visit = func(p *frontend.Package) {
		if p == nil || seen[p] {
			return
		}
		seen[p] = true
		// Deterministic import order.
		names := make([]string, 0, len(p.Imports))
		for ip := range p.Imports {
			names = append(names, ip)
		}
		sort.Strings(names)
		for _, ip := range names {
			dep := p.Imports[ip]
			if dep == nil || len(dep.Syntax) == 0 {
				continue
			}
			if dep.IsStdlib && !compileFromSource[dep.PkgPath] {
				continue // stdlib needs overlays/shims; handled separately
			}
			visit(dep)
		}
		order = append(order, p)
	}
	visit(root)
	return order
}

// curPrefix is the CLR-name prefix for the package currently being lowered.
func (c *lowerCtx) curPrefix() string { return c.prefixOf[c.pkg.Types] }

// funcObj returns the *types.Func an identifier refers to (nil if it is not a
// function reference).
func (l *funcLowerer) funcObj(id *ast.Ident) *types.Func {
	fn, _ := l.pkg.TypesInfo.Uses[id].(*types.Func)
	return fn
}

// globalRef reports whether e references a package-level variable (an ident or a
// pkg.Var selector), returning its global index.
func (l *funcLowerer) globalRef(e ast.Expr) (int, bool) {
	var id *ast.Ident
	switch x := e.(type) {
	case *ast.Ident:
		id = x
	case *ast.SelectorExpr:
		id = x.Sel
	default:
		return 0, false
	}
	if v, ok := l.pkg.TypesInfo.Uses[id].(*types.Var); ok {
		if gi, ok := l.globals[v]; ok {
			return gi, true
		}
	}
	return 0, false
}

// prefixForPkg returns the CLR-name prefix for a (possibly imported) types
// package, falling back to a mangled path for packages not in the lowered set.
func (c *lowerCtx) prefixForPkg(p *types.Package) string {
	if p == nil {
		return ""
	}
	if pre, ok := c.prefixOf[p]; ok {
		return pre
	}
	return manglePkgPath(p.Path()) + "_"
}

// manglePkgPath turns an import path into a CLR-safe identifier prefix.
func manglePkgPath(path string) string {
	r := strings.NewReplacer("/", "_", ".", "_", "-", "_", "@", "_")
	return r.Replace(path)
}

// methodShell builds a method signature from a func decl. A method becomes a
// static method named TypeName_Method with the receiver as its first parameter.
func (c *lowerCtx) methodShell(fd *ast.FuncDecl) (*goir.Method, bool) {
	prefix := c.curPrefix()
	m := &goir.Method{Name: prefix + fd.Name.Name, GoName: fd.Name.Name, Ret: goir.TVoid}

	if fd.Recv != nil {
		rt, ok := c.goType(c.pkg.TypesInfo.TypeOf(fd.Recv.List[0].Type))
		if !ok {
			return nil, c.unsupported(fd.Recv.Pos(), "receiver type")
		}
		m.Params = append(m.Params, rt)
		m.Name = prefix + c.recvTypeName(fd) + "_" + fd.Name.Name
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
		// Opaque shim types (reflect.Type/Value, sync.*, …) are handles backed by
		// runtime reference objects, not lowered structures.
		if isOpaqueShimType(named) {
			return goir.Type{Kind: goir.KObject, Shim: named.Obj().Pkg().Path() + "." + named.Obj().Name()}, true
		}
	}
	// A pointer to an opaque value-type shim IS that shim's runtime object
	// (*bytes.Buffer and bytes.Buffer share one handle).
	if pt, ok := t.Underlying().(*types.Pointer); ok {
		if named, ok := pt.Elem().(*types.Named); ok && isOpaqueShimType(named) {
			return goir.Type{Kind: goir.KObject, Shim: named.Obj().Pkg().Path() + "." + named.Obj().Name()}, true
		}
	}
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
	if arr, ok := t.Underlying().(*types.Array); ok {
		et, ok := c.goType(arr.Elem())
		if !ok {
			return goir.Type{}, false
		}
		// Fixed-size [N]T arrays are slice-backed but carry value semantics; the
		// Array flag drives copy-on-assignment in exprCoerced.
		at := goir.SliceType(et)
		at.Array = true
		at.ArrayLen = int(arr.Len())
		return at, true
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
	case types.Int8:
		return goir.Type{Kind: goir.KInt32, TruncOp: goir.OpConvI1}, true
	case types.Int16:
		return goir.Type{Kind: goir.KInt32, TruncOp: goir.OpConvI2}, true
	case types.UnsafePointer:
		// An opaque managed handle (only in shimmed/overlaid code).
		return goir.TObject, true
	case types.Uint8: // byte: indexing a string yields this
		return goir.Type{Kind: goir.KInt32, TruncOp: goir.OpConvU1}, true
	case types.Uint, types.Uint64, types.Uintptr:
		return goir.TUint64, true
	case types.Uint32:
		return goir.TUint32, true
	case types.Uint16:
		return goir.Type{Kind: goir.KUint32, TruncOp: goir.OpConvU2}, true
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
	name := c.prefixForPkg(named.Obj().Pkg()) + named.Obj().Name()
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
		s.Fields = append(s.Fields, goir.Field{Name: f.Name(), Type: ft, Tag: st.Tag(i)})
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
		s.Fields = append(s.Fields, goir.Field{Name: f.Name(), Type: ft, Tag: st.Tag(i)})
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

// funcLowerer lowers a single function body. It reads the current package's
// TypesInfo through the embedded lowerCtx (c.pkg), which the driver sets before
// each package's shell/body pass (lowering is sequential, so c.pkg is stable for
// the duration of one function and its inline closures).
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
	inClosure bool
	// closureRet is the first result type (TVoid if none); closureResults holds all
	// result types when the literal returns more than one (returned as an object[]
	// tuple, like a multi-result named function).
	closureRet     goir.Type
	closureResults []goir.Type
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
