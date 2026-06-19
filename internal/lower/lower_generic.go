package lower

import (
	"go/ast"
	"go/types"
	"strings"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// substType returns t with any type parameters replaced by their concrete types
// from subst, rebuilding composite types (slice/map/pointer/chan) so nested type
// parameters are substituted too. Forms outside the supported set pass through.
func substType(t types.Type, subst map[*types.TypeParam]types.Type) types.Type {
	if subst == nil {
		return t
	}
	switch u := t.(type) {
	case *types.TypeParam:
		if c, ok := subst[u]; ok {
			return c
		}
	case *types.Slice:
		return types.NewSlice(substType(u.Elem(), subst))
	case *types.Map:
		return types.NewMap(substType(u.Key(), subst), substType(u.Elem(), subst))
	case *types.Pointer:
		return types.NewPointer(substType(u.Elem(), subst))
	case *types.Chan:
		return types.NewChan(u.Dir(), substType(u.Elem(), subst))
	case *types.Named:
		// Re-instantiate a generic named type under the substitution, e.g.
		// Stack[T] -> Stack[int], so its concrete struct registers correctly.
		ta := u.TypeArgs()
		if ta == nil || ta.Len() == 0 {
			return t
		}
		args := make([]types.Type, ta.Len())
		changed := false
		for i := 0; i < ta.Len(); i++ {
			args[i] = substType(ta.At(i), subst)
			if args[i] != ta.At(i) {
				changed = true
			}
		}
		if !changed {
			return t
		}
		if inst, err := types.Instantiate(nil, u.Origin(), args, false); err == nil {
			return inst
		}
	}
	return t
}

// unqualified renders type names without package paths, for compact mangled
// instantiation names.
func unqualified(*types.Package) string { return "" }

// typeListString joins a type list for mangled instantiation names.
func typeListString(tl *types.TypeList) string {
	var b strings.Builder
	for i := 0; i < tl.Len(); i++ {
		if i > 0 {
			b.WriteByte(',')
		}
		b.WriteString(types.TypeString(tl.At(i), unqualified))
	}
	return b.String()
}

// goType (on funcLowerer) shadows lowerCtx.goType to apply the active generic
// type-parameter substitution before resolving. With no substitution in effect
// it is a straight pass-through, so non-generic lowering is unaffected.
func (l *funcLowerer) goType(t types.Type) (goir.Type, bool) {
	if l.typeSubst != nil {
		t = substType(t, l.typeSubst)
	}
	return l.lowerCtx.goType(t)
}

// fieldParamType (on funcLowerer) mirrors lowerCtx.fieldParamType but routes
// through the substituting goType so a generic parameter's `...T` / `T` resolves
// to the instantiation's concrete type.
func (l *funcLowerer) fieldParamType(field *ast.Field) (goir.Type, bool) {
	if ell, ok := field.Type.(*ast.Ellipsis); ok {
		et, ok := l.goType(l.pkg.TypesInfo.TypeOf(ell.Elt))
		if !ok {
			return goir.Type{}, false
		}
		return goir.SliceType(et), true
	}
	return l.goType(l.pkg.TypesInfo.TypeOf(field.Type))
}

// genericCallee resolves a call to a generic function to its monomorphized
// method, instantiating (and queuing the body) on first use. fun is the called
// identifier; its concrete type arguments come from the type checker.
func (l *funcLowerer) genericCallee(fun *ast.Ident) (*goir.Method, bool) {
	decl, isGeneric := l.genericDecls[fun.Name]
	if !isGeneric {
		return nil, false
	}
	inst, ok := l.pkg.TypesInfo.Instances[fun]
	if !ok || inst.TypeArgs == nil {
		l.fail(fun.Pos(), "generic call without inferred type arguments")
		return nil, false
	}

	subst := map[*types.TypeParam]types.Type{}
	var key strings.Builder
	key.WriteString(fun.Name)
	key.WriteByte('[')
	// Map each type parameter object to its concrete argument, in order.
	tpObjs := l.typeParamObjs(decl)
	for i, tp := range tpObjs {
		if i >= inst.TypeArgs.Len() {
			break
		}
		ca := inst.TypeArgs.At(i)
		subst[tp] = substType(ca, l.typeSubst) // resolve nested params from an outer instantiation
		if i > 0 {
			key.WriteByte(',')
		}
		key.WriteString(types.TypeString(subst[tp], unqualified))
	}
	key.WriteByte(']')
	k := key.String()

	if m, ok := l.monoInsts[k]; ok {
		return m, true
	}

	m, ok := l.shellWithSubst(decl, mangleMono(k), subst)
	if !ok {
		return nil, false
	}
	l.monoInsts[k] = m
	l.prog.Methods = append(l.prog.Methods, m)
	l.monoTodo = append(l.monoTodo, monoJob{decl: decl, method: m, subst: subst})
	return m, true
}

// namedOf returns the named type underneath a (possibly pointer) type, or nil.
func namedOf(t types.Type) *types.Named {
	if p, ok := t.(*types.Pointer); ok {
		t = p.Elem()
	}
	if n, ok := t.(*types.Named); ok {
		return n
	}
	return nil
}

// instantiateMethod resolves a call to a method on a generic type (e.g.
// st.Push where st is Stack[int]) to a monomorphized method, instantiating and
// queuing its body on first use.
func (l *funcLowerer) instantiateMethod(fn *types.Func, seln *types.Selection) (*goir.Method, bool) {
	orig := fn.Origin()
	decl := l.genericMethodDecls[orig]
	if decl == nil {
		decl = l.genericMethodDecls[fn]
	}
	if decl == nil {
		return nil, false
	}
	recvNamed := namedOf(seln.Recv())
	if recvNamed == nil || recvNamed.TypeArgs() == nil {
		return nil, false
	}
	targs := recvNamed.TypeArgs()
	rtps := orig.Type().(*types.Signature).RecvTypeParams()
	subst := map[*types.TypeParam]types.Type{}
	for i := 0; i < rtps.Len() && i < targs.Len(); i++ {
		subst[rtps.At(i)] = substType(targs.At(i), l.typeSubst)
	}

	key := types.TypeString(recvNamed, nil) + "." + decl.Name.Name
	if m, ok := l.monoInsts[key]; ok {
		return m, true
	}
	m, ok := l.methodShellSubst(decl, subst)
	if !ok {
		return nil, false
	}
	l.monoInsts[key] = m
	l.prog.Methods = append(l.prog.Methods, m)
	l.monoTodo = append(l.monoTodo, monoJob{decl: decl, method: m, subst: subst})
	return m, true
}

// methodShellSubst builds the signature of a monomorphized generic method: the
// receiver (param 0) plus parameters/results, all resolved through subst.
func (c *lowerCtx) methodShellSubst(decl *ast.FuncDecl, subst map[*types.TypeParam]types.Type) (*goir.Method, bool) {
	resolve := func(t types.Type) (goir.Type, bool) { return c.goType(substType(t, subst)) }

	rt, ok := resolve(c.pkg.TypesInfo.TypeOf(decl.Recv.List[0].Type))
	if !ok {
		return nil, c.unsupported(decl.Recv.Pos(), "generic receiver type")
	}
	base := rt
	if base.Kind == goir.KPtr {
		base = *base.Elem
	}
	recvName := "T"
	if base.Kind == goir.KStruct {
		recvName = base.Struct.Name
	}

	m := &goir.Method{Name: recvName + "_" + decl.Name.Name, Ret: goir.TVoid}
	m.GoName = m.Name
	m.Params = append(m.Params, rt)
	if decl.Type.Params != nil {
		for _, field := range decl.Type.Params.List {
			var pt goir.Type
			var ok bool
			if ell, isEll := field.Type.(*ast.Ellipsis); isEll {
				var et goir.Type
				et, ok = resolve(c.pkg.TypesInfo.TypeOf(ell.Elt))
				pt = goir.SliceType(et)
			} else {
				pt, ok = resolve(c.pkg.TypesInfo.TypeOf(field.Type))
			}
			if !ok {
				return nil, c.unsupported(field.Type.Pos(), "generic method parameter type")
			}
			n := len(field.Names)
			if n == 0 {
				n = 1
			}
			for i := 0; i < n; i++ {
				m.Params = append(m.Params, pt)
			}
		}
	}
	if decl.Type.Results != nil {
		for _, field := range decl.Type.Results.List {
			rrt, ok := resolve(c.pkg.TypesInfo.TypeOf(field.Type))
			if !ok {
				return nil, c.unsupported(field.Type.Pos(), "generic method result type")
			}
			n := len(field.Names)
			if n == 0 {
				n = 1
			}
			for i := 0; i < n; i++ {
				m.Results = append(m.Results, rrt)
			}
		}
	}
	switch len(m.Results) {
	case 0:
		m.Ret = goir.TVoid
	case 1:
		m.Ret = m.Results[0]
	default:
		m.Ret = goir.TObjectArray
	}
	return m, true
}

// typeParamObjs returns the *types.TypeParam objects of a generic func decl, in
// declaration order.
func (l *funcLowerer) typeParamObjs(decl *ast.FuncDecl) []*types.TypeParam {
	var out []*types.TypeParam
	if fn, ok := l.pkg.TypesInfo.Defs[decl.Name].(*types.Func); ok {
		if sig, ok := fn.Type().(*types.Signature); ok {
			tpl := sig.TypeParams()
			for i := 0; i < tpl.Len(); i++ {
				out = append(out, tpl.At(i))
			}
		}
	}
	return out
}

// shellWithSubst builds the method signature for a generic instantiation, with
// each parameter/result type resolved through the type-parameter substitution.
func (c *lowerCtx) shellWithSubst(decl *ast.FuncDecl, name string, subst map[*types.TypeParam]types.Type) (*goir.Method, bool) {
	resolve := func(t types.Type) (goir.Type, bool) { return c.goType(substType(t, subst)) }

	m := &goir.Method{Name: name, GoName: name, Ret: goir.TVoid}
	if decl.Type.Params != nil {
		for _, field := range decl.Type.Params.List {
			var pt goir.Type
			var ok bool
			if ell, isEll := field.Type.(*ast.Ellipsis); isEll {
				var et goir.Type
				et, ok = resolve(c.pkg.TypesInfo.TypeOf(ell.Elt))
				pt = goir.SliceType(et)
			} else {
				pt, ok = resolve(c.pkg.TypesInfo.TypeOf(field.Type))
			}
			if !ok {
				return nil, c.unsupported(field.Type.Pos(), "generic parameter type")
			}
			n := len(field.Names)
			if n == 0 {
				n = 1
			}
			for i := 0; i < n; i++ {
				m.Params = append(m.Params, pt)
			}
		}
	}
	if decl.Type.Results != nil {
		for _, field := range decl.Type.Results.List {
			rt, ok := resolve(c.pkg.TypesInfo.TypeOf(field.Type))
			if !ok {
				return nil, c.unsupported(field.Type.Pos(), "generic result type")
			}
			n := len(field.Names)
			if n == 0 {
				n = 1
			}
			for i := 0; i < n; i++ {
				m.Results = append(m.Results, rt)
			}
		}
	}
	switch len(m.Results) {
	case 0:
		m.Ret = goir.TVoid
	case 1:
		m.Ret = m.Results[0]
	default:
		m.Ret = goir.TObjectArray
	}
	return m, true
}

// mangleMono turns an instantiation key like "Min[int]" into a CLR-safe method
// name.
func mangleMono(key string) string {
	r := strings.NewReplacer("[", "__", "]", "", ",", "_", " ", "", ".", "_", "*", "p", "/", "_")
	return r.Replace(key)
}
