package analysis

import (
	"fmt"
	"html"
	"strings"
)

// RenderHTML writes a self-contained HTML compatibility report: a package-by-package
// table (status, kind, overlay, notes) plus the stdlib coverage summary and diagnostics.
// The output is deterministic (packages are already sorted) and has no external assets,
// so it can be published as-is (CI artifact, docs page).
func (r *Report) RenderHTML(sb *strings.Builder) {
	cov := r.StdlibSummary()
	var ok, warn, fail int
	for _, p := range r.Packages {
		switch p.Status {
		case "OK":
			ok++
		case "WARN":
			warn++
		case "FAIL":
			fail++
		}
	}

	fmt.Fprintf(sb, `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>goclr compatibility report — %s</title>
<style>
:root { color-scheme: light dark; }
body { font: 14px/1.5 system-ui, sans-serif; margin: 0; padding: 2rem; max-width: 1100px; margin-inline: auto; }
h1 { font-size: 1.4rem; margin: 0 0 .25rem; }
.sub { color: #666; margin: 0 0 1.5rem; }
.verdict { display: inline-block; padding: .15rem .6rem; border-radius: 999px; font-weight: 600; }
.verdict.ok { background: #e7f6e7; color: #176117; }
.verdict.bad { background: #fdeaea; color: #a11; }
.cards { display: flex; gap: 1rem; flex-wrap: wrap; margin: 1rem 0 2rem; }
.card { border: 1px solid #ddd; border-radius: 8px; padding: .75rem 1rem; min-width: 7rem; }
.card .n { font-size: 1.6rem; font-weight: 700; }
.card .l { color: #666; font-size: .8rem; text-transform: uppercase; letter-spacing: .04em; }
table { border-collapse: collapse; width: 100%%; }
th, td { text-align: left; padding: .4rem .6rem; border-bottom: 1px solid #eee; vertical-align: top; }
th { font-size: .75rem; text-transform: uppercase; letter-spacing: .04em; color: #666; }
code { font-family: ui-monospace, monospace; }
.badge { font-weight: 600; padding: .05rem .45rem; border-radius: 4px; font-size: .8rem; }
.badge.OK { background: #e7f6e7; color: #176117; }
.badge.WARN { background: #fff6e0; color: #8a6300; }
.badge.FAIL { background: #fdeaea; color: #a11; }
.notes { color: #555; }
.diag { font-family: ui-monospace, monospace; font-size: .85rem; white-space: pre-wrap; }
.muted { color: #888; }
</style>
</head>
<body>
<h1>goclr compatibility report</h1>
<p class="sub">profile <code>%s</code></p>
<p><span class="verdict %s">%s</span></p>
<div class="cards">
  <div class="card"><div class="n">%d</div><div class="l">OK</div></div>
  <div class="card"><div class="n">%d</div><div class="l">Warn</div></div>
  <div class="card"><div class="n">%d</div><div class="l">Fail</div></div>
  <div class="card"><div class="n">%d / %d / %d</div><div class="l">stdlib full / partial / pending</div></div>
</div>
<table>
<thead><tr><th>Status</th><th>Package</th><th>Kind</th><th>Overlay</th><th>Notes</th></tr></thead>
<tbody>
`,
		html.EscapeString(string(r.Profile)),
		html.EscapeString(string(r.Profile)),
		boolClass(r.Compatible), verdictText(r.Compatible),
		ok, warn, fail,
		cov.Full, cov.Partial, cov.Pending,
	)

	for _, p := range r.Packages {
		overlay := p.Overlay
		if overlay == "" {
			overlay = `<span class="muted">—</span>`
		} else {
			overlay = html.EscapeString(overlay)
		}
		notes := html.EscapeString(strings.Join(p.Notes, "; "))
		if len(p.Unsafe) > 0 {
			notes += fmt.Sprintf(" <span class=\"muted\">(%d unsafe site(s))</span>", len(p.Unsafe))
		}
		fmt.Fprintf(sb, "<tr><td><span class=\"badge %s\">%s</span></td><td><code>%s</code></td><td class=\"muted\">%s</td><td>%s</td><td class=\"notes\">%s</td></tr>\n",
			p.Status, p.Status, html.EscapeString(p.ImportPath), html.EscapeString(p.Kind), overlay, notes)
	}

	sb.WriteString("</tbody></table>\n")

	if len(r.Diagnostics) > 0 {
		sb.WriteString("<h2>Diagnostics</h2>\n")
		for _, d := range r.Diagnostics {
			loc := d.Pos.String()
			if loc != "" {
				loc = " (" + loc + ")"
			}
			sev := strings.ToUpper(d.Severity.String())
			fmt.Fprintf(sb, "<p class=\"diag\"><strong>%s</strong> %s: %s%s</p>\n",
				html.EscapeString(sev), html.EscapeString(string(d.Code)), html.EscapeString(d.Message), html.EscapeString(loc))
		}
	}

	sb.WriteString("</body>\n</html>\n")
}

func boolClass(ok bool) string {
	if ok {
		return "ok"
	}
	return "bad"
}

func verdictText(ok bool) string {
	if ok {
		return "compatible"
	}
	return "NOT compatible"
}
