package main
import ("fmt";"os";"path/filepath")
func main(){
 base:=filepath.Join(os.TempDir(),"goclr_osrd_test")
 os.RemoveAll(base); os.MkdirAll(filepath.Join(base,"sub"),0o755)
 os.WriteFile(filepath.Join(base,"a.txt"),[]byte("aaa"),0o644)
 os.WriteFile(filepath.Join(base,"Z.txt"),[]byte("zzzzz"),0o644)
 os.WriteFile(filepath.Join(base,"b.dat"),[]byte("bb"),0o644)
 ents,err:=os.ReadDir(base)
 fmt.Println("err",err,"n",len(ents))
 for _,e:=range ents{
  s:=""
  if !e.IsDir(){ info,_:=e.Info(); s=fmt.Sprintf("sz=%d",info.Size()) }
  fmt.Printf("%-6s dir=%v typeIsDir=%v %s\n",e.Name(),e.IsDir(),e.Type().IsDir(),s)
 }
 _,e2:=os.ReadDir(filepath.Join(base,"nope")); fmt.Println("missing",e2!=nil)
 os.RemoveAll(base)
}
