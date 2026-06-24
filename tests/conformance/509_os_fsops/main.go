package main
import ("fmt";"os";"path/filepath")
func main(){
 base:=filepath.Join(os.TempDir(),"goclr_osfs_test")
 os.RemoveAll(base); os.MkdirAll(base,0o755)
 // Chdir
 fmt.Println("chdir",os.Chdir(base))
 wd,_:=os.Getwd(); fmt.Println("wd-ok",filepath.Base(wd)=="goclr_osfs_test")
 fmt.Println("chdir-bad",os.Chdir(filepath.Join(base,"nope"))!=nil)
 // Truncate
 os.WriteFile("f.txt",[]byte("hello world"),0o644)
 fmt.Println("trunc",os.Truncate("f.txt",5))
 b,_:=os.ReadFile("f.txt"); fmt.Printf("after-trunc %q\n",b)
 fmt.Println("trunc-grow",os.Truncate("f.txt",8))
 b2,_:=os.ReadFile("f.txt"); fmt.Printf("grown len=%d\n",len(b2))
 fmt.Println("trunc-bad",os.Truncate("nope.txt",1)!=nil)
 // Symlink + Readlink
 fmt.Println("symlink",os.Symlink("f.txt","link.txt"))
 tgt,err:=os.Readlink("link.txt"); fmt.Println("readlink",tgt,err)
 lb,_:=os.ReadFile("link.txt"); fmt.Printf("via-link len=%d\n",len(lb))
 _,rlerr:=os.Readlink("f.txt"); fmt.Println("readlink-nonlink",rlerr!=nil)
 os.Chdir(os.TempDir()); os.RemoveAll(base)
}
