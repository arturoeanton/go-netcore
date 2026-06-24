package main
import ("fmt";"io/fs";"os";"path/filepath")
func main(){
 modes:=[]fs.FileMode{0,0o644,0o755,0o777,fs.ModeDir|0o755,fs.ModeSymlink|0o777,fs.ModeDir,fs.ModeNamedPipe|0o644,fs.ModeSocket,fs.ModeDevice|fs.ModeCharDevice,fs.ModeSetuid|0o755,fs.ModeSticky|0o777,fs.ModeAppend,fs.ModeTemporary|fs.ModeExclusive}
 for _,m:=range modes{ fmt.Printf("%010o -> %s\n",uint32(m),m.String()) }
 // FormatDirEntry via os.ReadDir
 base:=filepath.Join(os.TempDir(),"goclr_fmode_test")
 os.RemoveAll(base); os.MkdirAll(filepath.Join(base,"sub"),0o755)
 os.WriteFile(filepath.Join(base,"f.txt"),[]byte("x"),0o644)
 ents,_:=os.ReadDir(base)
 for _,e:=range ents{ fmt.Println(fs.FormatDirEntry(e)) }
 os.RemoveAll(base)
}
