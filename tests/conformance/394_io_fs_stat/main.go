// io/fs.Stat over os.DirFS: the fs.FS.Open call is driven through the interface
// method-callback bridge and an os.File-backed FileInfo is read back — a real stat
// (was a stub). A user fs.FS that can't be dispatched returns a clean not-found.
package main

import (
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
)

type stubFS struct{}

func (stubFS) Open(string) (fs.File, error) { return nil, fs.ErrNotExist }

func main() {
	dir, _ := os.MkdirTemp("", "goclrfs")
	defer os.RemoveAll(dir)
	os.WriteFile(filepath.Join(dir, "note.txt"), []byte("hello"), 0644)
	os.Mkdir(filepath.Join(dir, "sub"), 0755)

	dfs := os.DirFS(dir)
	if fi, err := fs.Stat(dfs, "note.txt"); err == nil {
		fmt.Printf("file: name=%s dir=%v\n", fi.Name(), fi.IsDir())
	} else {
		fmt.Println("file err:", err)
	}
	if fi, err := fs.Stat(dfs, "sub"); err == nil {
		fmt.Printf("dir: name=%s dir=%v\n", fi.Name(), fi.IsDir())
	}
	_, err := fs.Stat(dfs, "missing.txt")
	fmt.Println("missing not-found:", err != nil)

	_, e := fs.Stat(stubFS{}, "x")
	fmt.Println("stubfs not-found:", e != nil)
}
