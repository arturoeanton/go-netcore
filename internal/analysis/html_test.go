package analysis

import (
	"encoding/json"
	"strings"
	"testing"
)

func sampleReport() *Report {
	r := &Report{
		Profile: ProfileEchoGoja,
		Packages: []PackageVerdict{
			{ImportPath: "main", Kind: "main", Status: "OK"},
			{ImportPath: "encoding/json", Kind: "stdlib", Status: "OK", Overlay: "full"},
			{ImportPath: "crypto/tls", Kind: "stdlib", Status: "WARN", Overlay: "partial", Notes: []string{"TLS is a no-op shim"}},
			{ImportPath: "plugin", Kind: "stdlib", Status: "FAIL", Notes: []string{"unsupported on .NET"}},
		},
		Compatible: true,
	}
	r.Summary = r.computeSummary()
	return r
}

func TestComputeSummary(t *testing.T) {
	s := sampleReport().Summary
	if s.Packages != 4 || s.OK != 2 || s.Warn != 1 || s.Fail != 1 {
		t.Errorf("summary = %+v, want 4/2/1/1", s)
	}
	if s.Stdlib.Full != 1 || s.Stdlib.Partial != 1 || s.Stdlib.Pending != 1 {
		t.Errorf("stdlib coverage = %+v, want 1/1/1", s.Stdlib)
	}
}

func TestRenderHTML(t *testing.T) {
	var sb strings.Builder
	sampleReport().RenderHTML(&sb)
	out := sb.String()
	for _, want := range []string{
		"<!doctype html>",
		"goclr compatibility report",
		"echo-goja",
		`<span class="verdict ok">compatible</span>`,
		`<code>encoding/json</code>`,
		`<span class="badge FAIL">FAIL</span>`,
		"TLS is a no-op shim",
		"</html>",
	} {
		if !strings.Contains(out, want) {
			t.Errorf("HTML report missing %q", want)
		}
	}
	// Notes are HTML-escaped (no raw injection).
	if strings.Contains(out, "<script>") {
		t.Errorf("unexpected raw markup in report")
	}
}

func TestReportJSONStable(t *testing.T) {
	b, err := json.Marshal(sampleReport())
	if err != nil {
		t.Fatal(err)
	}
	// The stable headline fields consumers depend on must be present.
	for _, want := range []string{`"profile"`, `"summary"`, `"packages"`, `"compatible"`, `"stdlib"`} {
		if !strings.Contains(string(b), want) {
			t.Errorf("JSON report missing field %q", want)
		}
	}
}
