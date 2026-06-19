package diagnostics

import "testing"

func TestBagSeverityAndOrdering(t *testing.T) {
	b := &Bag{}
	b.Add(New(SeverityWarn, CodeStdlibMissing, "partial").WithPackage("z"))
	b.Add(New(SeverityError, CodeCgoImport, "cgo").WithPackage("a"))
	b.Add(New(SeverityInfo, CodeStdlibMissing, "info").WithPackage("m"))

	if !b.HasErrors() {
		t.Fatal("HasErrors() = false, want true")
	}
	info, warn, errs := b.Counts()
	if info != 1 || warn != 1 || errs != 1 {
		t.Fatalf("Counts() = (%d,%d,%d), want (1,1,1)", info, warn, errs)
	}

	sorted := b.Sorted()
	if sorted[0].Severity != SeverityError {
		t.Errorf("first sorted diagnostic severity = %v, want error", sorted[0].Severity)
	}
}

func TestPositionString(t *testing.T) {
	cases := map[Position]string{
		{File: "f.go", Line: 12, Col: 5}: "f.go:12:5",
		{File: "f.go", Line: 12}:         "f.go:12",
		{File: "f.go"}:                   "f.go",
		{}:                               "",
	}
	for pos, want := range cases {
		if got := pos.String(); got != want {
			t.Errorf("Position(%+v).String() = %q, want %q", pos, got, want)
		}
	}
}

func TestSeverityID(t *testing.T) {
	d := New(SeverityError, CodeEmitFailure, "boom")
	if d.SeverityID != "error" {
		t.Errorf("SeverityID = %q, want error", d.SeverityID)
	}
}
