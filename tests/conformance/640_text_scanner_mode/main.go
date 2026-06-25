package main

import (
	"fmt"
	"strings"
	"text/scanner"
)

// text/scanner.Scanner.Mode is a Go uint field; setting it (s.Mode = ...) previously
// failed with a signature mismatch (the setter took a long, the compiler emits a
// ulong). Default-mode scanning and custom modes both work now.
func main() {
	// default GoTokens mode tokenizes Go-like source
	var s scanner.Scanner
	s.Init(strings.NewReader("foo := 42 + bar * 3.14 // comment\n\"hi\""))
	for tok := s.Scan(); tok != scanner.EOF; tok = s.Scan() {
		fmt.Printf("%s:%q ", scanner.TokenString(tok), s.TokenText())
	}
	fmt.Println()

	// read Mode back and test flag bits
	fmt.Println(s.Mode == scanner.GoTokens)
	s.Mode = scanner.ScanIdents | scanner.ScanInts
	fmt.Println(s.Mode&scanner.ScanIdents != 0, s.Mode&scanner.ScanFloats != 0)

	// idents + ints only
	var s2 scanner.Scanner
	s2.Init(strings.NewReader("abc 123 def 45.6"))
	s2.Mode = scanner.ScanIdents | scanner.ScanInts | scanner.ScanFloats
	var toks []string
	for tok := s2.Scan(); tok != scanner.EOF; tok = s2.Scan() {
		toks = append(toks, s2.TokenText())
	}
	fmt.Println(toks)

	// comments preserved
	var s3 scanner.Scanner
	s3.Init(strings.NewReader("a /* c */ b // line\nd"))
	s3.Mode = scanner.ScanIdents | scanner.ScanComments
	for tok := s3.Scan(); tok != scanner.EOF; tok = s3.Scan() {
		fmt.Printf("%q ", s3.TokenText())
	}
	fmt.Println()

	// strings / chars / raw strings
	var s4 scanner.Scanner
	s4.Init(strings.NewReader(`"str" 'c' ` + "`raw`"))
	s4.Mode = scanner.ScanStrings | scanner.ScanChars | scanner.ScanRawStrings
	for tok := s4.Scan(); tok != scanner.EOF; tok = s4.Scan() {
		fmt.Printf("%s ", s4.TokenText())
	}
	fmt.Println()

	// Peek / Next, and position
	var s5 scanner.Scanner
	s5.Init(strings.NewReader("a\nbb\nccc"))
	for tok := s5.Scan(); tok != scanner.EOF; tok = s5.Scan() {
		fmt.Printf("%s@%d:%d ", s5.TokenText(), s5.Pos().Line, s5.Pos().Column)
	}
	fmt.Println()
}
