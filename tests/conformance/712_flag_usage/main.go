package main

import (
	"flag"
	"fmt"
	"os"
)

// flag.FlagSet error handling: a Parse failure prints the error message and the
// usage block (Go's failf -> defaultUsage -> PrintDefaults) to the set's output
// before returning the error, and -help prints the usage and returns ErrHelp.
// PrintDefaults formatting (indent, "-name type", default annotation, back-quoted
// usage names) must match go run byte-for-byte.
func try(name string, defs func(*flag.FlagSet), args []string) {
	fs := flag.NewFlagSet(name, flag.ContinueOnError)
	fs.SetOutput(os.Stdout)
	defs(fs)
	err := fs.Parse(args)
	fmt.Printf(">> err=%v\n", err)
}

func main() {
	// Standalone PrintDefaults across kinds, defaults, and a back-quoted usage name.
	fs := flag.NewFlagSet("tool", flag.ContinueOnError)
	fs.SetOutput(os.Stdout)
	fs.String("opt", "x", "an option here")
	fs.Int("num", 0, "a number")
	fs.Bool("flag", false, "a `b`ool flag")
	fs.Float64("ratio", 1.5, "the ratio")
	fs.Duration("timeout", 0, "how long")
	fmt.Println("== PrintDefaults ==")
	fs.PrintDefaults()

	fmt.Println("== errors ==")
	try("a", func(fs *flag.FlagSet) { fs.Int("n", 0, "a num") }, []string{"-n", "notanumber"})
	try("b", func(fs *flag.FlagSet) { fs.String("s", "", "a str") }, []string{"-s"})
	try("c", func(fs *flag.FlagSet) { fs.Bool("v", false, "verbose") }, []string{"-v=maybe"})
	try("", func(fs *flag.FlagSet) { fs.Int("x", 0, "x val") }, []string{"-bad"})
	try("d", func(fs *flag.FlagSet) { fs.String("o", "", "opt") }, []string{"-help"})
	try("e", func(fs *flag.FlagSet) { fs.Float64("r", 0, "ratio") }, []string{"-r", "x.y"})
	try("f", func(fs *flag.FlagSet) { fs.String("known", "d", "k") }, []string{"-unknown", "v"})

	// A successful parse prints nothing; the values and leftover args are returned.
	fs2 := flag.NewFlagSet("ok", flag.ContinueOnError)
	fs2.SetOutput(os.Stdout)
	n := fs2.Int("count", 1, "count")
	fmt.Println(fs2.Parse([]string{"-count", "9", "rest1", "rest2"}), *n, fs2.Args())
}
