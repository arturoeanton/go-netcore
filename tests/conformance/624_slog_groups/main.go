package main

import (
	"bytes"
	"fmt"
	"log/slog"
)

// log/slog grouping: (*Logger).WithGroup nests all later attrs under a group, and
// slog.Group(key, ...) inlines a sub-group attribute. Both render as nested objects
// in JSON and dotted keys in text, and compose with each other and With.
func jsonLogger(buf *bytes.Buffer) *slog.Logger {
	return slog.New(slog.NewJSONHandler(buf, &slog.HandlerOptions{
		ReplaceAttr: func(g []string, a slog.Attr) slog.Attr {
			if a.Key == slog.TimeKey {
				return slog.Attr{}
			}
			return a
		},
	}))
}

func textLogger(buf *bytes.Buffer) *slog.Logger {
	return slog.New(slog.NewTextHandler(buf, &slog.HandlerOptions{
		ReplaceAttr: func(g []string, a slog.Attr) slog.Attr {
			if a.Key == slog.TimeKey {
				return slog.Attr{}
			}
			return a
		},
	}))
}

func main() {
	var b bytes.Buffer
	// With before a group stays at top level; attrs after nest under the group.
	jsonLogger(&b).With("service", "api").WithGroup("req").Info("handled", "method", "GET", "status", 200)
	// nested groups
	jsonLogger(&b).WithGroup("outer").With("a", 1).WithGroup("inner").Info("m", "b", 2, "c", 3)
	// With after WithGroup nests under the group
	jsonLogger(&b).With("top", 1).WithGroup("g").With("mid", 2).Info("m", "leaf", 3)
	// inline slog.Group
	jsonLogger(&b).Info("m", slog.Group("user", "name", "alice", "age", 30), "ok", true)
	// slog.Group inside a WithGroup
	jsonLogger(&b).WithGroup("req").Info("m", slog.Group("hdr", "ct", "json"), "code", 200)
	// empty WithGroup is a no-op
	jsonLogger(&b).WithGroup("").Info("m", "x", 1)
	fmt.Print(b.String())

	var t bytes.Buffer
	// text handler -> dotted keys
	textLogger(&t).WithGroup("req").With("id", "x").WithGroup("hdr").Info("m", "ct", "json")
	textLogger(&t).WithGroup("a").WithGroup("b").Info("m", "k", "v")
	textLogger(&t).Info("m", slog.Group("grp", slog.Int("x", 1), slog.String("y", "z")))
	fmt.Print(t.String())
}
