package lower

import "strings"

// CoverageData is goclr's stdlib coverage, exported for the per-function coverage
// matrix (`goclr coverage`). It enumerates what the backend actually implements:
// the shimmed package funcs, the shimmed methods (per declaring type), the shimmed
// package vars/typed-consts, and the packages lowered wholesale from real Go source.
type CoverageData struct {
	Funcs              map[string]map[string]bool // import path -> {func name}
	Methods            map[string]map[string]bool // import path -> {"Type.Method"}
	Vars               map[string]map[string]bool // import path -> {var/const name}
	CompiledFromSource map[string]bool             // import path lowered from source (full)
}

// Coverage snapshots the backend's shim registries and compiled-from-source set.
func Coverage() CoverageData {
	cd := CoverageData{
		Funcs:              map[string]map[string]bool{},
		Methods:            map[string]map[string]bool{},
		Vars:               map[string]map[string]bool{},
		CompiledFromSource: map[string]bool{},
	}
	for pkg, funcs := range shimRegistry {
		set := map[string]bool{}
		for name := range funcs {
			set[name] = true
		}
		cd.Funcs[pkg] = set
	}
	for key, methods := range shimMethodRegistry {
		pkg, typ := splitQualified(key)
		if cd.Methods[pkg] == nil {
			cd.Methods[pkg] = map[string]bool{}
		}
		for m := range methods {
			cd.Methods[pkg][typ+"."+m] = true
		}
	}
	for key := range shimVarRegistry {
		pkg, name := splitQualified(key)
		if cd.Vars[pkg] == nil {
			cd.Vars[pkg] = map[string]bool{}
		}
		cd.Vars[pkg][name] = true
	}
	for pkg := range compileFromSource {
		cd.CompiledFromSource[pkg] = true
	}
	return cd
}

// splitQualified splits a "import/path.Name" registry key into its import path and
// trailing identifier. The separator is the final dot (import paths use slashes, and
// no stdlib type/var name contains a dot).
func splitQualified(key string) (pkg, name string) {
	i := strings.LastIndex(key, ".")
	if i < 0 {
		return key, ""
	}
	return key[:i], key[i+1:]
}

// TargetedPackages is every stdlib import path the backend covers at least partially
// (a shimmed func/method/var, or compiled from source) — the default scope for the
// coverage matrix.
func TargetedPackages() []string {
	seen := map[string]bool{}
	for pkg := range shimRegistry {
		seen[pkg] = true
	}
	for key := range shimMethodRegistry {
		pkg, _ := splitQualified(key)
		seen[pkg] = true
	}
	for key := range shimVarRegistry {
		pkg, _ := splitQualified(key)
		seen[pkg] = true
	}
	for pkg := range compileFromSource {
		seen[pkg] = true
	}
	out := make([]string, 0, len(seen))
	for pkg := range seen {
		out = append(out, pkg)
	}
	return out
}
