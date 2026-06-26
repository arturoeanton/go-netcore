package main

import (
	"fmt"
	"io/fs"
	"os"
)

// fs.FileMode / os.FileMode (an alias): String() drives %v/%s and Println; Perm/Type and
// bit-ops keep the FileMode identity; the alias resolves to io/fs.FileMode for %T, methods,
// constants, and as a struct field.
type FI struct{ Mode os.FileMode }

func main() {
	var m fs.FileMode = 0o644
	fmt.Println(m.String(), m.IsDir(), m.IsRegular(), m.Perm())
	fmt.Printf("%v %s %T\n", m, m, m)

	d := fs.ModeDir | 0o755
	fmt.Println(d.String(), d.IsDir(), d.Perm())

	// os.FileMode alias
	var om os.FileMode = 0o600
	fmt.Printf("%T %v %s\n", om, om, om)
	fmt.Println(om.Perm(), om&0o777, om|os.ModeDir)

	fmt.Println(fs.ModePerm, os.ModeDir|os.ModePerm)

	// as a struct field
	fmt.Printf("%v %+v\n", FI{0o644}, FI{fs.ModeDir | 0o700})
}
