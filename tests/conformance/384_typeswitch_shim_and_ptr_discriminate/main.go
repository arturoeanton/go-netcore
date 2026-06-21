// A type switch must not let an opaque shim type (time.Time) match a plain boxed
// value (int64 was wrongly captured because shim types lower to System.Object), and a
// type switch/assertion over pointers-to-non-struct must tell *int64 from *string from
// *bool by the pointee representation. Both are exactly what database/sql's Scan needs.
package main

import (
	"fmt"
	"time"
)

func kind(v any) string {
	switch v.(type) {
	case time.Time:
		return "time"
	case int64:
		return "int64"
	case float64:
		return "float64"
	case string:
		return "string"
	default:
		return "other"
	}
}

// assign mirrors database/sql convertAssign's pointer-dest type switch.
func assign(dest, src any) string {
	switch d := dest.(type) {
	case *string:
		*d = src.(string)
		return "string"
	case *int64:
		*d = src.(int64)
		return "int64"
	case *bool:
		*d = src.(bool)
		return "bool"
	default:
		return "?"
	}
}

func isInt64Ptr(v any) bool { _, ok := v.(*int64); return ok }

func main() {
	fmt.Println(kind(int64(42)))    // int64, NOT time
	fmt.Println(kind(3.5))          // float64
	fmt.Println(kind("hi"))         // string
	fmt.Println(kind(time.Unix(0, 0).UTC())) // time

	var n int64
	var s string
	var b bool
	fmt.Println(assign(&n, int64(7)), n)
	fmt.Println(assign(&s, "x"), s)
	fmt.Println(assign(&b, true), b)

	fmt.Println(isInt64Ptr(&n)) // true
	fmt.Println(isInt64Ptr(&s)) // false (was wrongly true)
}
