package main

import (
	"flag"
	"os"
)

func main() {
	fs := flag.NewFlagSet("app", flag.ContinueOnError)
	fs.SetOutput(os.Stdout)
	fs.Bool("v", false, "verbose `mode` on")
	fs.Int("level", 3, "the logging level")
	fs.String("name", "anon", "your `username` here")
	fs.String("empty", "", "no default shown")
	fs.Float64("ratio", 1.5, "the ratio")
	fs.Duration("timeout", 0, "request timeout")
	fs.Bool("x", true, "single-letter bool default true")
	fs.PrintDefaults()
}
