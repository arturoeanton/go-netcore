package main

import (
	"fmt"
	"log/slog"
)

// slog.Level.String(): canonical names and signed offsets ("INFO", "INFO+2", "WARN-1",
// "DEBUG+4"), and fmt's %v/%s of a Level dispatch the same String(); Level.Level() returns
// the receiver.
func main() {
	fmt.Println(slog.LevelDebug.String(), slog.LevelInfo.String(), slog.LevelWarn.String(), slog.LevelError.String())
	fmt.Println(slog.Level(2).String(), slog.Level(-2).String(), slog.Level(5).String(), slog.Level(1).String())
	fmt.Println(slog.Level(10).String(), slog.Level(-6).String(), slog.Level(3).String())

	fmt.Println(slog.LevelInfo, slog.LevelError, slog.LevelWarn)
	var l slog.Level = slog.LevelWarn
	fmt.Printf("%v %s %d\n", l, l, l)
	fmt.Println(slog.LevelInfo.Level())

	// level constants' integer values
	fmt.Println(int(slog.LevelDebug), int(slog.LevelInfo), int(slog.LevelWarn), int(slog.LevelError))
}
