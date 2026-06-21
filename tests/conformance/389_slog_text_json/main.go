// log/slog: structured text and JSON logging through a *slog.Logger, with attrs,
// With-preset fields, level filtering and slog.Attr arguments. A ReplaceAttr that
// drops the time key makes the output deterministic (the shim omits time anyway).
package main

import (
	"log/slog"
	"os"
)

func main() {
	noTime := &slog.HandlerOptions{
		ReplaceAttr: func(groups []string, a slog.Attr) slog.Attr {
			if a.Key == slog.TimeKey {
				return slog.Attr{}
			}
			return a
		},
	}

	log := slog.New(slog.NewTextHandler(os.Stdout, noTime))
	log.Info("server up", "port", 8080, "secure", false)
	log.Warn("disk space", "used_pct", 87.5)
	log.Debug("verbose", "skipped", true) // below Info: suppressed
	log.With("request", "r-1").Error("handler failed", "status", 500, slog.String("op", "read"))

	j := slog.New(slog.NewJSONHandler(os.Stdout, noTime))
	j.Info("event", "user", "alice", "count", 3)
}
