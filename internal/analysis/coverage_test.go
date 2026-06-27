package analysis

import "testing"

func TestComputeCoverage_ShimmedPackage(t *testing.T) {
	// strings is now fully covered (every shimmed func implemented), so use bytes — still a
	// partially-covered shimmed package — to exercise the partial-coverage reporting path.
	rep, err := ComputeCoverage([]string{"bytes"}, true)
	if err != nil {
		t.Fatalf("ComputeCoverage: %v", err)
	}
	if len(rep.Packages) != 1 {
		t.Fatalf("want 1 package, got %d", len(rep.Packages))
	}
	p := rep.Packages[0]
	if p.ImportPath != "bytes" {
		t.Fatalf("want bytes, got %q", p.ImportPath)
	}
	if p.Total == 0 || p.Covered == 0 || p.Covered >= p.Total {
		t.Fatalf("strings should be partially covered: covered=%d total=%d", p.Covered, p.Total)
	}
	// A core shimmed func is covered; the Go-1.24 iterator helper SplitSeq is too, now
	// that range-over-func is lowered (it compiles from source and is consumable).
	want := map[string]bool{"Split": true, "Join": true, "SplitSeq": true}
	got := map[string]bool{}
	for _, s := range p.Symbols {
		got[s.Name] = s.Covered
	}
	for name, covered := range want {
		if g, ok := got[name]; !ok {
			t.Errorf("symbol %q not enumerated", name)
		} else if g != covered {
			t.Errorf("symbol %q covered=%v, want %v", name, g, covered)
		}
	}
}

func TestComputeCoverage_CompiledFromSourceIsFull(t *testing.T) {
	rep, err := ComputeCoverage([]string{"sort"}, false)
	if err != nil {
		t.Fatalf("ComputeCoverage: %v", err)
	}
	if len(rep.Packages) != 1 {
		t.Fatalf("want 1 package, got %d", len(rep.Packages))
	}
	p := rep.Packages[0]
	if !p.FullSource {
		t.Fatalf("sort should be compiled from source")
	}
	if p.Percent() != 100 || p.Covered != p.Total {
		t.Fatalf("a full-source package must be 100%%: covered=%d total=%d", p.Covered, p.Total)
	}
}

func TestDefaultCoveragePackages_NonEmptySorted(t *testing.T) {
	pkgs := DefaultCoveragePackages()
	if len(pkgs) < 20 {
		t.Fatalf("expected the targeted set to be sizable, got %d", len(pkgs))
	}
	for i := 1; i < len(pkgs); i++ {
		if pkgs[i-1] > pkgs[i] {
			t.Fatalf("packages not sorted: %q before %q", pkgs[i-1], pkgs[i])
		}
	}
}
