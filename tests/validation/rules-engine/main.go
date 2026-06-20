// rules-engine: a small interface-driven rules engine — the kind of pluggable
// business logic a SaaS uses for pricing, eligibility, or validation. Exercises
// interface dispatch, type switches, slices of interfaces, and composition.
package main

import (
	"fmt"
	"sort"
)

type Facts map[string]float64

type Rule interface {
	Name() string
	Eval(Facts) (bool, string)
}

type Threshold struct {
	Key   string
	Min   float64
	Label string
}

func (t Threshold) Name() string { return "threshold:" + t.Key }
func (t Threshold) Eval(f Facts) (bool, string) {
	v := f[t.Key]
	if v >= t.Min {
		return true, fmt.Sprintf("%s ok (%.1f >= %.1f)", t.Label, v, t.Min)
	}
	return false, fmt.Sprintf("%s fail (%.1f < %.1f)", t.Label, v, t.Min)
}

type AllOf struct {
	Title string
	Rules []Rule
}

func (a AllOf) Name() string { return "allof:" + a.Title }
func (a AllOf) Eval(f Facts) (bool, string) {
	for _, r := range a.Rules {
		if ok, msg := r.Eval(f); !ok {
			return false, "blocked by " + r.Name() + ": " + msg
		}
	}
	return true, a.Title + " satisfied"
}

type Engine struct{ rules []Rule }

func (e *Engine) Add(r Rule) { e.rules = append(e.rules, r) }
func (e *Engine) Run(f Facts) map[string]bool {
	out := map[string]bool{}
	for _, r := range e.rules {
		ok, msg := r.Eval(f)
		out[r.Name()] = ok
		fmt.Printf("[%v] %-22s %s\n", ok, r.Name(), msg)
	}
	return out
}

func main() {
	e := &Engine{}
	e.Add(Threshold{Key: "credit", Min: 700, Label: "Credit score"})
	e.Add(Threshold{Key: "income", Min: 50000, Label: "Annual income"})
	e.Add(AllOf{Title: "loan-eligibility", Rules: []Rule{
		Threshold{Key: "credit", Min: 680, Label: "Credit"},
		Threshold{Key: "income", Min: 40000, Label: "Income"},
		Threshold{Key: "debt_ratio", Min: -0.4, Label: "Debt ratio"},
	}})

	facts := Facts{"credit": 720, "income": 48000, "debt_ratio": -0.3}
	results := e.Run(facts)

	keys := make([]string, 0, len(results))
	for k := range results {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	passed := 0
	for _, k := range keys {
		if results[k] {
			passed++
		}
	}
	fmt.Printf("passed %d/%d rules\n", passed, len(results))
}
