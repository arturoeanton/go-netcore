package main

import (
	"fmt"
	"io/fs"
	"time"
)

// A user type implementing io/fs.FileInfo must dispatch to ITS OWN methods through the
// interface — not be cast to the shim's GoFileInfo handle.
type memInfo struct {
	name string
	size int64
	dir  bool
}

func (m memInfo) Name() string       { return m.name }
func (m memInfo) Size() int64        { return m.size }
func (m memInfo) Mode() fs.FileMode  { return 0644 }
func (m memInfo) ModTime() time.Time { return time.Time{} }
func (m memInfo) IsDir() bool        { return m.dir }
func (m memInfo) Sys() any           { return nil }

type memFile struct{ info memInfo }

func (f *memFile) Stat() (fs.FileInfo, error) { return f.info, nil }
func (f *memFile) Read(p []byte) (int, error) { return 0, fmt.Errorf("eof") }
func (f *memFile) Close() error                { return nil }

// A user fs.FS whose Open returns the program's own fs.File/fs.FileInfo types.
type memFS map[string]int64

func (m memFS) Open(name string) (fs.File, error) {
	sz, ok := m[name]
	if !ok {
		return nil, fmt.Errorf("not found: %s", name)
	}
	return &memFile{info: memInfo{name: name, size: sz}}, nil
}

func describe(fi fs.FileInfo) string {
	return fmt.Sprintf("%s size=%d dir=%v", fi.Name(), fi.Size(), fi.IsDir())
}

func main() {
	// Direct dispatch through the fs.FileInfo interface on a user type.
	var fi fs.FileInfo = memInfo{name: "x.dat", size: 7}
	fmt.Println("direct:", describe(fi))

	var dirInfo fs.FileInfo = memInfo{name: "sub", dir: true}
	fmt.Println("dir:", describe(dirInfo))

	// Through fs.Stat over a user fs.FS (Open -> file.Stat, both via the callback bridge).
	var fsys fs.FS = memFS{"a.txt": 5, "b.txt": 10}
	for _, n := range []string{"a.txt", "b.txt", "missing"} {
		info, err := fs.Stat(fsys, n)
		if err != nil {
			fmt.Println("stat", n, "-> error")
			continue
		}
		fmt.Println("stat", n, "->", describe(info))
	}
}
