package main
import ("fmt";"go/token")
func main(){
 fs:=token.NewFileSet()
 src:="package main\nfunc main(){\n\tprintln(1)\n}\n"
 f:=fs.AddFile("a.go",fs.Base(),len(src))
 fmt.Println("base",f.Base(),"size",f.Size(),"name",f.Name(),"fsbase",fs.Base())
 // add lines at each \n offset
 for i,c:=range src{ if c=='\n'{ f.AddLine(i+1) } }
 fmt.Println("linecount",f.LineCount())
 // positions
 for _,off:=range []int{0,5,13,26,len(src)-1}{
  p:=f.Pos(off)
  pos:=fs.Position(p)
  fmt.Printf("off=%d pos=%s line=%d col=%d offset=%d back=%d\n",off,pos.String(),f.Line(p),pos.Column,f.Offset(p),int(p))
 }
 // second file gets a disjoint base
 g:=fs.AddFile("b.go",-1,20)
 fmt.Println("g.base",g.Base(),"fsbase",fs.Base())
 // FileSet.File finds the right file
 fmt.Println("file-of",fs.File(g.Pos(5)).Name(),fs.File(f.Pos(0)).Name())
 // LineStart
 fmt.Println("linestart2",int(f.LineStart(2)))
}
