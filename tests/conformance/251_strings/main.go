package main

import "strings"

func main() {
	println(strings.ToUpper("hello"), strings.ToLower("WORLD"))
	println(strings.Contains("seafood", "foo"), strings.HasPrefix("golang", "go"), strings.HasSuffix("golang", "ng"))
	println(strings.Index("chicken", "ken"), strings.Count("cheese", "e"))
	println(strings.Repeat("ab", 3))
	println(strings.Replace("oink oink oink", "k", "ky", 2))
	println(strings.ReplaceAll("a,b,c", ",", "-"))
	println(strings.TrimSpace("  hi  "), strings.TrimPrefix("__file", "__"))
	parts := strings.Split("a,b,c,d", ",")
	println(len(parts), parts[0], parts[3])
	println(strings.Join(parts, "+"))
	fields := strings.Fields("  foo bar  baz   ")
	println(len(fields), fields[0], fields[2])
	println(strings.EqualFold("Go", "GO"), strings.LastIndex("go gopher", "go"))
}
