package main

import (
	"flag"
	"fmt"
)

func main() {
	fs := flag.NewFlagSet("app", flag.ContinueOnError)
	verbose := fs.Bool("verbose", false, "verbose output")
	level := fs.Int("level", 3, "log level")
	name := fs.String("name", "anon", "the name")

	var collected []string
	fs.Func("tag", "append a tag", func(s string) error {
		collected = append(collected, s)
		return nil
	})

	err := fs.Parse([]string{"-verbose", "-level", "7", "-tag", "a", "-tag", "b", "rest"})
	fmt.Println("err", err, "v", *verbose, "lvl", *level, "name", *name)
	fmt.Println("tags", collected)
	fmt.Println("args", fs.Args())

	// VisitAll (all defined, sorted) — prints name + default.
	fmt.Println("-- VisitAll --")
	fs.VisitAll(func(f *flag.Flag) {
		fmt.Printf("%s default=%q usage=%q\n", f.Name, f.DefValue, f.Usage)
	})
	// Visit (only set, sorted).
	fmt.Println("-- Visit --")
	fs.Visit(func(f *flag.Flag) {
		fmt.Printf("set %s\n", f.Name)
	})

	// Lookup.
	if lf := fs.Lookup("level"); lf != nil {
		fmt.Println("lookup level default", lf.DefValue)
	}
	fmt.Println("lookup missing nil:", fs.Lookup("nope") == nil)
}
