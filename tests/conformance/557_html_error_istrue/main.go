package main

import (
	"fmt"
	"html/template"
)

func main() {
	// html/template.Error formatting across the Name/Line/Description cases.
	e1 := &template.Error{Name: "page", Line: 12, Description: "bad pipeline"}
	e2 := &template.Error{Name: "layout", Description: "predefined escaper"}
	e3 := &template.Error{Description: "generic problem"}
	fmt.Println(e1.Error())
	fmt.Println(e2.Error())
	fmt.Println(e3.Error())
	fmt.Println("code/name/line:", e1.ErrorCode, e1.Name, e1.Line)

	// IsTrue across kinds.
	for _, v := range []any{0, 1, "", "x", []int{}, []int{1}, false, true, 0.0, 3.14, nil, struct{}{}} {
		t, ok := template.IsTrue(v)
		fmt.Printf("%v -> truth=%v ok=%v\n", v, t, ok)
	}
}
