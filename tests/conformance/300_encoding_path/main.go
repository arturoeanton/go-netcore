package main
import ("fmt";"encoding/hex";"encoding/base64";"path";"path/filepath")
func main(){
	h := hex.EncodeToString([]byte("hello")); fmt.Println(h)
	d,_ := hex.DecodeString(h); fmt.Println(string(d))
	fmt.Println(base64.StdEncoding.EncodeToString([]byte("hello world")))
	fmt.Println(base64.URLEncoding.EncodeToString([]byte{0xff,0xfe,0xfd}))
	b,_ := base64.StdEncoding.DecodeString("aGVsbG8="); fmt.Println(string(b))
	fmt.Println(path.Join("a","b","c"), path.Join("a/","/b"), path.Clean("a/../b/./c"))
	fmt.Println(path.Base("/a/b/c.txt"), path.Dir("/a/b/c.txt"), path.Ext("/a/b/c.txt"))
	fmt.Println(filepath.Join("x","y"), filepath.Base("/p/q.go"), filepath.Ext("f.tar.gz"))
	fmt.Println(path.Clean("/../a/b/../c"), path.IsAbs("/x"), path.IsAbs("x"))
}
