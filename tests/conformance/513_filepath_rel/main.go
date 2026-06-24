package main
import ("fmt";"path/filepath")
func main(){
 rels:=[][2]string{{"/a/b","/a/b/c/d"},{"/a/b/c","/a/b"},{"/a/b","/a/c"},{"a/b","a/b/c"},{"/a","/a"},{"/a/b/c","/a/b/c"},{"a","b"},{"/x","y"},{"/a/./b","/a/b/c"},{".","a/b"}}
 for _,r:=range rels{
  out,err:=filepath.Rel(r[0],r[1])
  fmt.Printf("Rel(%q,%q)=%q err=%v\n",r[0],r[1],out,err)
 }
 fmt.Println("split",filepath.SplitList("/a:/b:/c"),filepath.SplitList(""),filepath.SplitList("/x"))
 fmt.Println("vol",filepath.VolumeName("/a/b")=="" )
 fmt.Println("local",filepath.IsLocal("a/b"),filepath.IsLocal("/a"),filepath.IsLocal("../a"),filepath.IsLocal("a/../b"),filepath.IsLocal("a/../../b"),filepath.IsLocal(""),filepath.IsLocal("."))
 fmt.Println("hasprefix",filepath.HasPrefix("/a/b","/a"),filepath.HasPrefix("/a/b","/x"))
}
