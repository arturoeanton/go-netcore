package main
import ("fmt";"go/token")
func main(){
 fs:=token.NewFileSet()
 content:=[]byte("line one\nline two\nline three\nfour\n")
 f:=fs.AddFile("x.go",fs.Base(),len(content))
 f.SetLinesForContent(content)
 fmt.Println("lines",f.Lines(),"count",f.LineCount(),"end",int(f.End()))
 // positions per line
 for ln:=1;ln<=f.LineCount();ln++{
  p:=f.LineStart(ln)
  fmt.Printf("line %d start=%d pos=%s\n",ln,int(p),fs.Position(p).String())
 }
 // MergeLine merges line 2 into 1
 f.MergeLine(2)
 fmt.Println("after-merge",f.Lines(),"count",f.LineCount())
 // Iterate
 fs.AddFile("y.go",-1,5)
 var names []string
 fs.Iterate(func(file *token.File)bool{ names=append(names,file.Name()); return true })
 fmt.Println("iterate",names)
 // Iterate early stop
 var first string
 fs.Iterate(func(file *token.File)bool{ first=file.Name(); return false })
 fmt.Println("first",first)
}
