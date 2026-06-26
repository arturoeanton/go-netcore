package main

import (
	"fmt"
	"net/url"
	"os"
	"path/filepath"
)

// Struct-field reads on shim types that were unpopulated: url.URL's RawPath/RawFragment/
// ForceQuery, and os.FileInfo's Mode (permission bits) and ModTime (a real instant).
func main() {
	// url.URL fields.
	for _, raw := range []string{
		"https://u:p@h.com:8443/a%2Fb/c?q=1#frag",
		"http://x/path?", // ForceQuery
		"http://x/plain",
		"mailto:a@b.com",
		"https://h/p#sec%20tion",
	} {
		u, _ := url.Parse(raw)
		fmt.Printf("%-40s sch=%q host=%q path=%q rawpath=%q rawq=%q frag=%q rawfrag=%q force=%v opaque=%q\n",
			raw, u.Scheme, u.Host, u.Path, u.RawPath, u.RawQuery, u.Fragment, u.RawFragment, u.ForceQuery, u.Opaque)
		fmt.Println("  roundtrip:", u.String())
	}

	// os.FileInfo Mode + ModTime.
	dir, _ := os.MkdirTemp("", "fi-*")
	defer os.RemoveAll(dir)
	for _, tc := range []struct {
		name string
		perm os.FileMode
	}{{"a.txt", 0644}, {"b.sh", 0755}, {"c.dat", 0600}, {"d.ro", 0444}} {
		fp := filepath.Join(dir, tc.name)
		os.WriteFile(fp, []byte("data"), tc.perm)
		fi, _ := os.Stat(fp)
		fmt.Printf("%s mode=%v perm=%o isdir=%v regular=%v size=%d modzero=%v\n",
			tc.name, fi.Mode(), fi.Mode().Perm(), fi.IsDir(), fi.Mode().IsRegular(), fi.Size(), fi.ModTime().IsZero())
	}
	// Directory mode.
	di, _ := os.Stat(dir)
	fmt.Printf("dir isdir=%v dirbit=%v\n", di.IsDir(), di.Mode().IsDir())

	// WriteFile applies perm (modulo umask) on create only; an overwrite keeps the perm.
	for _, p := range []os.FileMode{0666, 0777, 0640, 0700} {
		fp := filepath.Join(dir, fmt.Sprintf("w%o", p))
		os.WriteFile(fp, []byte("x"), p)
		fi, _ := os.Stat(fp)
		fmt.Printf("write %o -> %o\n", p, fi.Mode().Perm())
	}
	ov := filepath.Join(dir, "ov")
	os.WriteFile(ov, []byte("a"), 0600)
	os.WriteFile(ov, []byte("bb"), 0755)
	fiov, _ := os.Stat(ov)
	fmt.Printf("overwrite perm=%o\n", fiov.Mode().Perm())
}
