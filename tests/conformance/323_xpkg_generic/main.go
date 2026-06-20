package main

import (
	"fmt"

	"github.com/arturoeanton/go-netcore/tests/conformance/323_xpkg_generic/genutil"
)

func main() {
	s := []int{3, 1, 2, 5, 4}
	genutil.Sort(s)
	fmt.Println(s, genutil.Max(s))

	ss := []string{"banana", "apple", "cherry"}
	genutil.Sort(ss)
	fmt.Println(ss, genutil.Max(ss))

	doubled := genutil.Map(s, func(x int) int { return x * 2 })
	fmt.Println(doubled)
	labels := genutil.Map(s, func(x int) string { return fmt.Sprintf("n%d", x) })
	fmt.Println(labels)
}
