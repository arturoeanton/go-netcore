package main
import ("fmt";"go/token")
func main(){
 fs:=token.NewFileSet()
 content:=[]byte("aaaa\nbbbb\ncccc\ndddd\neeee\n")
 f:=fs.AddFile("orig.go",fs.Base(),len(content))
 f.SetLinesForContent(content)
 // line directive: at offset 10 (line 3), pretend it's gen.go line 100
 f.AddLineInfo(10,"gen.go",100)
 f.AddLineColumnInfo(20,"other.go",50,7)
 for _,off:=range []int{0,5,10,15,20,24}{
  p:=f.Pos(off)
  fmt.Printf("off=%d adj=%s raw=%s\n",off,fs.Position(p).String(),fs.PositionFor(p,false).String())
 }
}
