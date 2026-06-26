package main

import (
	"fmt"
	"os"
	"path/filepath"
)

// os.OpenFile honors the O_APPEND / O_TRUNC / O_CREATE / O_EXCL flags (decoded with the
// host platform's os.O_* values, which goclr shares with Go): O_APPEND writes extend the
// file, O_TRUNC truncates, O_CREATE|O_EXCL fails (EEXIST, os.IsExist) if the path exists,
// and a missing file under O_RDONLY is ENOENT (os.IsNotExist). Previously every write
// mode opened at position 0, so O_APPEND overwrote from the start.
func main() {
	dir, err := os.MkdirTemp("", "goclr-of-*")
	if err != nil {
		fmt.Println("mktemp", err)
		return
	}
	defer os.RemoveAll(dir)
	fp := filepath.Join(dir, "f.txt")

	os.WriteFile(fp, []byte("hello\n"), 0644)

	// O_APPEND extends the file
	af, _ := os.OpenFile(fp, os.O_APPEND|os.O_WRONLY, 0644)
	af.WriteString("world\n")
	af.WriteString("again\n")
	af.Close()
	d, _ := os.ReadFile(fp)
	fmt.Printf("append: %q\n", d)

	// O_TRUNC truncates
	tf, _ := os.OpenFile(fp, os.O_TRUNC|os.O_WRONLY, 0644)
	tf.WriteString("XY")
	tf.Close()
	d2, _ := os.ReadFile(fp)
	fmt.Printf("trunc: %q\n", d2)

	// O_CREATE|O_EXCL: new ok, existing fails with IsExist
	nf := filepath.Join(dir, "new.txt")
	_, e1 := os.OpenFile(nf, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0644)
	fmt.Println("excl-new:", e1 == nil)
	_, e2 := os.OpenFile(nf, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0644)
	fmt.Println("excl-exists:", e2 != nil, os.IsExist(e2))

	// O_RDONLY of a missing file: IsNotExist
	_, e3 := os.OpenFile(filepath.Join(dir, "nope.txt"), os.O_RDONLY, 0)
	fmt.Println("rdonly-missing:", os.IsNotExist(e3))

	// O_RDWR|O_APPEND
	rf, _ := os.OpenFile(fp, os.O_RDWR|os.O_APPEND, 0644)
	rf.WriteString("Z")
	rf.Close()
	d3, _ := os.ReadFile(fp)
	fmt.Printf("rdwr-append: %q\n", d3)
}
