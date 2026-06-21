package lower

// This test is a compile-time guard for the shim registries. Every entry maps a Go
// stdlib symbol to a {csType, csMethod} pair the compiler emits an extern call to; if
// the C# method does not exist in that exact class (a typo, a wrong class such as
// Http vs HttpTypes, or a renamed/removed method) the program only fails at JIT with a
// MissingMethodException — far from the registry edit that caused it. Here we parse the
// runtime C# sources and assert every registry pair resolves to a public static method
// in the named class, turning that whole class of runtime failure into a test failure.
//
// Scope note: this validates existence + class + cross-assembly callability (public).
// Parameter/return type compatibility is a planned second tier (it needs the IR types
// the compiler derives per call site, not just the registry).

import (
	"os"
	"path/filepath"
	"sort"
	"strings"
	"testing"
)

// csMethod is a parsed C# method: its enclosing class and whether it is callable from
// another assembly (public — the emitted program lives in a different assembly).
type csMethodInfo struct {
	public bool
}

// parseCSharpMethods returns, for every C# source file under the given dirs, a map
// "Class.Method" -> info covering each `static` method declaration. Comments and string
// literals are stripped first so braces inside them never skew the class-nesting depth.
func parseCSharpMethods(t *testing.T, dirs ...string) map[string]csMethodInfo {
	t.Helper()
	out := map[string]csMethodInfo{}
	for _, dir := range dirs {
		entries, err := os.ReadDir(dir)
		if err != nil {
			t.Fatalf("reading %s: %v", dir, err)
		}
		for _, e := range entries {
			if e.IsDir() || !strings.HasSuffix(e.Name(), ".cs") {
				continue
			}
			src, err := os.ReadFile(filepath.Join(dir, e.Name()))
			if err != nil {
				t.Fatalf("reading %s: %v", e.Name(), err)
			}
			parseCSFile(string(src), out)
		}
	}
	return out
}

// parseCSFile tokenizes one C# file and records its static methods keyed by enclosing
// class. The tokenizer drops //, /* */, "..." (incl. @verbatim and $interpolated) and
// '...' so only structural tokens remain.
func parseCSFile(src string, out map[string]csMethodInfo) {
	type token struct {
		text string // an identifier, or one of { } ( ; for punctuation
		kind byte   // 'i' ident, '{' '}' '(' ';'
	}
	var toks []token
	i, n := 0, len(src)
	isIdent := func(c byte) bool {
		return c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
	}
	for i < n {
		c := src[i]
		switch {
		case c == '/' && i+1 < n && src[i+1] == '/':
			for i < n && src[i] != '\n' {
				i++
			}
		case c == '/' && i+1 < n && src[i+1] == '*':
			i += 2
			for i+1 < n && !(src[i] == '*' && src[i+1] == '/') {
				i++
			}
			i += 2
		case c == '"':
			verbatim := i > 0 && (src[i-1] == '@' || (i > 1 && src[i-1] == '$' && src[i-2] == '@') || (i > 1 && src[i-1] == '@' && src[i-2] == '$'))
			i++
			for i < n {
				if verbatim {
					if src[i] == '"' {
						if i+1 < n && src[i+1] == '"' { // "" -> escaped quote
							i += 2
							continue
						}
						i++
						break
					}
					i++
				} else {
					if src[i] == '\\' {
						i += 2
						continue
					}
					if src[i] == '"' {
						i++
						break
					}
					i++
				}
			}
		case c == '\'':
			i++
			for i < n {
				if src[i] == '\\' {
					i += 2
					continue
				}
				if src[i] == '\'' {
					i++
					break
				}
				i++
			}
		case c == '{' || c == '}' || c == '(' || c == ';':
			toks = append(toks, token{kind: c})
			i++
		case isIdent(c):
			j := i
			for j < n && isIdent(src[j]) {
				j++
			}
			toks = append(toks, token{text: src[i:j], kind: 'i'})
			i = j
		default:
			i++
		}
	}

	// Second pass: track class nesting by brace depth and record static methods.
	type cls struct {
		name  string
		depth int
	}
	var stack []cls
	depth := 0
	pendingClass := "" // a `class X` seen, opens at the next '{'
	// Per-declaration accumulators, reset at any structural boundary.
	sawStatic, sawPublic := false, false
	resetDecl := func() { sawStatic, sawPublic = false, false }

	for k := 0; k < len(toks); k++ {
		tk := toks[k]
		switch tk.kind {
		case '{':
			depth++
			if pendingClass != "" {
				stack = append(stack, cls{pendingClass, depth})
				pendingClass = ""
			}
			resetDecl()
		case '}':
			if len(stack) > 0 && stack[len(stack)-1].depth == depth {
				stack = stack[:len(stack)-1]
			}
			depth--
			resetDecl()
		case ';':
			resetDecl()
		case 'i':
			switch tk.text {
			case "public":
				sawPublic = true
			case "static":
				sawStatic = true
			case "class", "struct", "interface", "enum":
				if k+1 < len(toks) && toks[k+1].kind == 'i' {
					pendingClass = toks[k+1].text
				}
			}
		case '(':
			// A `... static NAME (` where NAME is the preceding identifier is a method.
			if sawStatic && k >= 1 && toks[k-1].kind == 'i' && len(stack) > 0 {
				name := toks[k-1].text
				if name != "class" && name != "struct" {
					key := stack[len(stack)-1].name + "." + name
					if cur, ok := out[key]; !ok || (!cur.public && sawPublic) {
						out[key] = csMethodInfo{public: sawPublic}
					}
				}
			}
			resetDecl()
		}
	}
}

// allRegistryPairs collects every (csType, csMethod) the compiler may emit a call to,
// tagged with the registry + Go symbol for a precise failure message.
func allRegistryPairs() []struct{ reg, sym, csType, csMethod string } {
	var pairs []struct{ reg, sym, csType, csMethod string }
	add := func(reg, sym string, sf shimFunc) {
		if sf.csType == "" && sf.csMethod == "" {
			return
		}
		pairs = append(pairs, struct{ reg, sym, csType, csMethod string }{reg, sym, sf.csType, sf.csMethod})
	}
	for pkg, m := range shimRegistry {
		for name, sf := range m {
			add("shimRegistry", pkg+"."+name, sf)
		}
	}
	for typ, m := range shimMethodRegistry {
		for name, sf := range m {
			add("shimMethodRegistry", typ+"."+name, sf)
		}
	}
	for typ, m := range shimFieldRegistry {
		for name, sf := range m {
			add("shimFieldRegistry", typ+"."+name, sf)
		}
	}
	for typ, m := range shimFieldSetRegistry {
		for name, sf := range m {
			add("shimFieldSetRegistry", typ+"."+name, sf)
		}
	}
	for sym, sf := range shimVarRegistry {
		add("shimVarRegistry", sym, sf)
	}
	for sym, sf := range opaqueZeroCtor {
		add("opaqueZeroCtor", sym, sf)
	}
	for sym, sf := range opaqueShimClone {
		add("opaqueShimClone", sym, sf)
	}
	return pairs
}

// TestShimRegistriesResolveToRealMethods asserts every registry entry maps to a public
// static C# method in the exact class named — the compile-time guard against the
// MissingMethodException class of bugs.
func TestShimRegistriesResolveToRealMethods(t *testing.T) {
	methods := parseCSharpMethods(t,
		filepath.Join("..", "..", "runtime", "stdlib"),
		filepath.Join("..", "..", "runtime", "dotnet", "Runtime"),
	)
	if len(methods) == 0 {
		t.Fatal("parsed zero C# methods — parser or paths are wrong")
	}

	var missing, notPublic []string
	for _, p := range allRegistryPairs() {
		key := p.csType + "." + p.csMethod
		info, ok := methods[key]
		if !ok {
			missing = append(missing, p.reg+": "+p.sym+" -> "+key+" (no such public static method)")
			continue
		}
		if !info.public {
			notPublic = append(notPublic, p.reg+": "+p.sym+" -> "+key+" (exists but is not public; not callable cross-assembly)")
		}
	}
	sort.Strings(missing)
	sort.Strings(notPublic)
	if len(missing) > 0 {
		t.Errorf("%d shim registry entries reference a missing C# method:\n  %s", len(missing), strings.Join(missing, "\n  "))
	}
	if len(notPublic) > 0 {
		t.Errorf("%d shim registry entries reference a non-public C# method:\n  %s", len(notPublic), strings.Join(notPublic, "\n  "))
	}
}
