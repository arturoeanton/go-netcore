package lower

// Compile-time guards for the shim registries / emitted externs against the runtime C#.
//
// Tier 1 (TestShimRegistriesResolveToRealMethods) — every registry entry must name a
// public static method in the exact class given. Catches a wrong class (Http vs
// HttpTypes), a typo, or a removed method: bugs that otherwise surface only as a
// MissingMethodException at JIT, far from the registry edit.
//
// Tier 2 (TestShimExternSignaturesMatchCSharp) — every extern the compiler actually
// EMITS (captured via the GOCLR_SHIM_MANIFEST instrumentation while building a corpus)
// must have parameter and return CLR types matching the target C# method. Catches the
// other half: a return/parameter type the compiler derives that the C# shim does not
// declare (e.g. URL_Query lowering to GoMap while the method returns object). The CLR
// resolves a MemberRef by exact signature, so any divergence is a runtime crash.

import (
	"bufio"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"testing"
)

// ---- C# source parsing ------------------------------------------------------------

// csSig is a parsed C# static method: callability + its CLR parameter and return types.
type csSig struct {
	public bool
	params []string // normalized CLR type per parameter
	ret    string   // normalized CLR return type
}

func parseCSharpMethods(t *testing.T, dirs ...string) map[string]csSig {
	t.Helper()
	out := map[string]csSig{}
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

// parseCSFile tokenizes one C# file (dropping comments and string/char literals so braces
// inside them never skew the class-nesting depth) and records each static method keyed by
// "Class.Method", with its parameter/return CLR types sliced from the raw source.
func parseCSFile(src string, out map[string]csSig) {
	type token struct {
		text string // identifier text, or "" for punctuation
		kind byte   // 'i' ident, or one of { } ( ) ; ,
		off  int    // byte offset of the token start
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
						if i+1 < n && src[i+1] == '"' {
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
		case c == '{' || c == '}' || c == '(' || c == ')' || c == ';' || c == ',':
			toks = append(toks, token{kind: c, off: i})
			i++
		case isIdent(c):
			j := i
			for j < n && isIdent(src[j]) {
				j++
			}
			toks = append(toks, token{text: src[i:j], kind: 'i', off: i})
			i = j
		default:
			i++
		}
	}

	type cls struct {
		name  string
		depth int
	}
	var stack []cls
	depth := 0
	pendingClass := ""
	declStart := 0 // byte offset just after the last structural boundary (;{}( ))
	sawStatic, sawPublic := false, false
	reset := func() { sawStatic, sawPublic = false, false }

	for k := 0; k < len(toks); k++ {
		tk := toks[k]
		switch tk.kind {
		case '{':
			depth++
			if pendingClass != "" {
				stack = append(stack, cls{pendingClass, depth})
				pendingClass = ""
			}
			reset()
			declStart = tk.off + 1
		case '}', ';', ')', ',':
			if tk.kind == '}' {
				if len(stack) > 0 && stack[len(stack)-1].depth == depth {
					stack = stack[:len(stack)-1]
				}
				depth--
			}
			reset()
			declStart = tk.off + 1
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
			// `... static NAME (` — a method. NAME is the preceding identifier.
			if sawStatic && k >= 1 && toks[k-1].kind == 'i' && len(stack) > 0 {
				name := toks[k-1].text
				// Find the matching ')'.
				dep, close := 1, -1
				for j := k + 1; j < len(toks); j++ {
					if toks[j].kind == '(' {
						dep++
					} else if toks[j].kind == ')' {
						dep--
						if dep == 0 {
							close = j
							break
						}
					}
				}
				if close >= 0 {
					retText := src[declStart:toks[k-1].off]
					paramText := src[tk.off+1 : toks[close].off]
					key := stack[len(stack)-1].name + "." + name
					sig := csSig{public: sawPublic, ret: normCSReturn(retText), params: splitParams(paramText)}
					if cur, ok := out[key]; !ok || (!cur.public && sig.public) {
						out[key] = sig
					}
				}
			}
			reset()
			declStart = tk.off + 1
		}
	}
}

// normCSReturn extracts and normalizes the CLR return type from the text preceding a
// method name ("public static GoMap " -> "GoMap").
func normCSReturn(text string) string {
	fields := strings.Fields(text)
	// Drop modifier keywords; the type is what remains nearest the name.
	mods := map[string]bool{"public": true, "private": true, "internal": true, "protected": true,
		"static": true, "sealed": true, "override": true, "virtual": true, "abstract": true,
		"async": true, "new": true, "unsafe": true, "extern": true, "readonly": true, "partial": true}
	var ty string
	for _, f := range fields {
		if !mods[f] {
			ty = f
		}
	}
	return normCSType(ty)
}

// splitParams splits a C# parameter list into normalized CLR parameter types.
func splitParams(text string) []string {
	text = strings.TrimSpace(text)
	if text == "" {
		return nil
	}
	var parts []string
	depth, last := 0, 0
	for i := 0; i < len(text); i++ {
		switch text[i] {
		case '<', '(', '[':
			depth++
		case '>', ')', ']':
			depth--
		case ',':
			if depth == 0 {
				parts = append(parts, text[last:i])
				last = i + 1
			}
		}
	}
	parts = append(parts, text[last:])
	out := make([]string, 0, len(parts))
	for _, p := range parts {
		out = append(out, paramType(p))
	}
	return out
}

// paramType extracts the normalized CLR type from a single parameter ("params object?[]
// args" -> "object[]", "GoString key" -> "GoString").
func paramType(p string) string {
	p = strings.TrimSpace(p)
	if eq := strings.IndexByte(p, '='); eq >= 0 { // drop default value
		p = strings.TrimSpace(p[:eq])
	}
	fields := strings.Fields(p)
	// Drop leading modifiers; the parameter name is the last field, the type the rest.
	for len(fields) > 0 && (fields[0] == "params" || fields[0] == "this" || fields[0] == "ref" || fields[0] == "out" || fields[0] == "in") {
		fields = fields[1:]
	}
	if len(fields) < 2 {
		if len(fields) == 1 {
			return normCSType(fields[0])
		}
		return ""
	}
	return normCSType(strings.Join(fields[:len(fields)-1], " "))
}

// normCSType normalizes a CLR type for comparison with the IR-derived name: strip the
// nullable '?' annotation (CLR-irrelevant) and surrounding whitespace.
func normCSType(t string) string {
	return strings.ReplaceAll(strings.TrimSpace(t), "?", "")
}

// ---- Tier 1: existence + class + callability --------------------------------------

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

func csharpDirs() []string {
	return []string{
		filepath.Join("..", "..", "runtime", "stdlib"),
		filepath.Join("..", "..", "runtime", "dotnet", "Runtime"),
	}
}

func TestShimRegistriesResolveToRealMethods(t *testing.T) {
	methods := parseCSharpMethods(t, csharpDirs()...)
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

// ---- Tier 2: emitted extern signatures vs C# --------------------------------------

// extern is one line of the GOCLR_SHIM_MANIFEST: a distinct emitted extern.
type extern struct {
	typ, method string
	params      []string // IR-derived CLR types
	ret         string
}

// knownSignatureGaps baselines shim externs whose emitted IR type and C# declaration
// diverge (the compiler intends a specific type — GoMap, uint, void, … — where the shim
// declares object/long). It is the ratchet: a NEW divergence fails the test, and a fixed
// entry is reported as "stale" so the baseline can only shrink. Keyed by "Class.Method".
//
// All of the original 38 divergences have been aligned to the emitted types — the
// baseline is empty. Keep it that way: fix the shim, don't add an entry.
var knownSignatureGaps = map[string]bool{}

// shimCorpus are packages whose compilation exercises a broad shim surface. Each is built
// with GOCLR_SHIM_MANIFEST set so the externs it emits are validated. Missing ones (e.g.
// examples that need a vendored tree) are skipped.
var shimCorpus = []string{
	"./examples/demo_echo",
	"./examples/demo_gin",
	"./examples/demo_gin_sql",
	"./tests/validation/goja",
	"./tests/validation/business-json",
	"./tests/validation/http-basic",
}

func TestShimExternSignaturesMatchCSharp(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping shim-signature corpus build in -short mode")
	}
	root, err := filepath.Abs(filepath.Join("..", ".."))
	if err != nil {
		t.Fatal(err)
	}
	goclr := filepath.Join(t.TempDir(), "goclr")
	if out, err := exec.Command("go", "build", "-o", goclr, "github.com/arturoeanton/go-netcore/cmd/goclr").CombinedOutput(); err != nil {
		t.Fatalf("building goclr: %v\n%s", err, out)
	}
	manifest := filepath.Join(t.TempDir(), "manifest.tsv")
	built := 0
	for _, pkg := range shimCorpus {
		if _, err := os.Stat(filepath.Join(root, filepath.FromSlash(strings.TrimPrefix(pkg, "./")), "main.go")); err != nil {
			continue
		}
		cmd := exec.Command(goclr, "build", pkg, "-o", filepath.Join(t.TempDir(), "out.dll"))
		cmd.Dir = root
		cmd.Env = append(os.Environ(), "GOCLR_SHIM_MANIFEST="+manifest)
		if out, err := cmd.CombinedOutput(); err != nil {
			t.Logf("skip %s (did not build): %v\n%s", pkg, err, lastLines(string(out), 3))
			continue
		}
		built++
	}
	if built == 0 {
		t.Skip("no corpus package built (vendored deps likely absent) — run `go mod vendor` to enable")
	}

	externs := readManifest(t, manifest)
	if len(externs) == 0 {
		t.Fatal("manifest empty after building corpus")
	}
	methods := parseCSharpMethods(t, csharpDirs()...)

	mismatched := map[string]string{} // key "Class.Method" -> message
	record := func(e extern, sig csSig, what string) {
		mismatched[e.typ+"."+e.method] = sigMsg(e, sig, what)
	}
	for _, e := range externs {
		sig, ok := methods[e.typ+"."+e.method]
		if !ok {
			continue // existence is Tier 1's job; only validate signatures we can resolve
		}
		if len(sig.params) != len(e.params) {
			record(e, sig, "parameter count")
			continue
		}
		if typeMismatch(e.ret, sig.ret) {
			record(e, sig, "return type")
			continue
		}
		for i := range e.params {
			if typeMismatch(e.params[i], sig.params[i]) {
				record(e, sig, "parameter "+strconv.Itoa(i+1)+" type")
				break
			}
		}
	}

	// New divergences (not baselined) fail; baselined entries that no longer diverge are
	// reported as stale so the baseline ratchets down as the shims are fixed.
	var added, stale []string
	for key, msg := range mismatched {
		if !knownSignatureGaps[key] {
			added = append(added, msg)
		}
	}
	for key := range knownSignatureGaps {
		if _, ok := mismatched[key]; !ok {
			stale = append(stale, key)
		}
	}
	sort.Strings(added)
	sort.Strings(stale)
	if len(added) > 0 {
		t.Errorf("%d NEW shim extern(s) disagree with the C# signature (align the C# method to the emitted type):\n  %s",
			len(added), strings.Join(added, "\n  "))
	}
	if len(stale) > 0 {
		t.Errorf("%d knownSignatureGaps entr(ies) no longer diverge — remove them from the baseline:\n  %s",
			len(stale), strings.Join(stale, "\n  "))
	}
}

// typeMismatch reports whether an IR-derived CLR type and a parsed C# type disagree.
// "struct" (a user value type) and types containing generics are skipped — the IR side
// can't name them and the C# side is rarely a shim parameter; the common scalar/handle
// vocabulary (object, GoString, GoSlice, GoMap, GoPtr, long, …) is matched exactly.
func typeMismatch(ir, cs string) bool {
	if ir == "struct" || cs == "" || strings.ContainsAny(cs, "<>") {
		return false
	}
	return ir != cs
}

func sigMsg(e extern, sig csSig, what string) string {
	return e.typ + "." + e.method + ": " + what +
		" — emitted (" + strings.Join(e.params, ",") + ")->" + e.ret +
		" vs C# (" + strings.Join(sig.params, ",") + ")->" + sig.ret
}

func readManifest(t *testing.T, path string) []extern {
	t.Helper()
	f, err := os.Open(path)
	if err != nil {
		return nil
	}
	defer f.Close()
	seen := map[string]bool{}
	var out []extern
	sc := bufio.NewScanner(f)
	sc.Buffer(make([]byte, 1<<20), 1<<20)
	for sc.Scan() {
		line := sc.Text()
		if seen[line] {
			continue
		}
		seen[line] = true
		f := strings.Split(line, "\t")
		if len(f) != 5 {
			continue
		}
		var params []string
		if f[3] != "" {
			params = strings.Split(f[3], ",")
		}
		out = append(out, extern{typ: f[1], method: f[2], params: params, ret: f[4]})
	}
	return out
}

func lastLines(s string, n int) string {
	lines := strings.Split(strings.TrimRight(s, "\n"), "\n")
	if len(lines) > n {
		lines = lines[len(lines)-n:]
	}
	return strings.Join(lines, "\n")
}
