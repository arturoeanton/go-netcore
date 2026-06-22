package main

import (
	"encoding/json"
	"encoding/xml"
	"errors"
	"fmt"
	"io"
)

// myErr is a real user error type — it must still dispatch precisely.
type myErr struct{ msg string }

func (e *myErr) Error() string { return e.msg }

// classify type-switches over the opaque-shim json/xml error types. None of them may
// falsely capture an unrelated error (the bug: every error matched *json.SyntaxError via
// the loose shim heuristic); a real *myErr must still match its own case.
func classify(err error) string {
	switch err.(type) {
	case *json.SyntaxError:
		return "json-syntax"
	case *json.UnmarshalTypeError:
		return "json-unmarshal"
	case *xml.SyntaxError:
		return "xml-syntax"
	case *xml.UnsupportedTypeError:
		return "xml-unsupported"
	case *myErr:
		return "my"
	default:
		return "other"
	}
}

func main() {
	fmt.Println(classify(errors.New("boom")))
	fmt.Println(classify(fmt.Errorf("wrap: %w", io.EOF)))
	fmt.Println(classify(io.EOF))
	fmt.Println(classify(&myErr{"x"}))
	fmt.Println(classify(nil))
}
