package main

import (
	"flag"
	"fmt"
	"strings"
)

// flag.Flag.Value is a flag.Value: for a built-in flag, String() reports the current
// value and Set() parses; for a Var flag, it is the user's own flag.Value.
type listValue []string

func (l *listValue) String() string     { return strings.Join(*l, ",") }
func (l *listValue) Set(s string) error { *l = append(*l, s); return nil }

func main() {
	fs := flag.NewFlagSet("test", flag.ContinueOnError)
	name := fs.String("name", "default", "the name")
	count := fs.Int("count", 1, "the count")
	verbose := fs.Bool("verbose", false, "verbose")
	fs.Parse([]string{"-name", "alice", "-count=5", "-verbose", "x", "y"})
	fmt.Println(*name, *count, *verbose, fs.Args(), fs.NArg(), fs.NFlag())

	// Visit only set flags, in lexicographic order, reading Value.String()
	var visited []string
	fs.Visit(func(f *flag.Flag) { visited = append(visited, f.Name+"="+f.Value.String()) })
	fmt.Println(visited)

	// VisitAll over every flag, with current value
	fs.VisitAll(func(f *flag.Flag) { fmt.Printf("%s(%s)=%s ", f.Name, f.DefValue, f.Value.String()) })
	fmt.Println()

	// Lookup + Value.Set through the interface
	f := fs.Lookup("count")
	f.Value.Set("42")
	fmt.Println(*count, f.Value.String())

	// custom flag.Value via Var
	fs2 := flag.NewFlagSet("t2", flag.ContinueOnError)
	var lst listValue
	fs2.Var(&lst, "item", "items")
	fs2.Parse([]string{"-item", "a", "-item", "b"})
	fmt.Println(lst, fs2.Lookup("item").Value.String())
}
