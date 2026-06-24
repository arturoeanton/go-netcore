package main
import ("fmt";"go/token")
func main(){
 ps:=[]token.Position{
  {Filename:"main.go",Offset:10,Line:3,Column:5},
  {Filename:"x.go",Line:7,Column:0},
  {Line:42,Column:8},
  {Line:9},
  {Filename:"only.go"},
  {},
 }
 for _,p:=range ps{
  fmt.Printf("%q valid=%v file=%q off=%d line=%d col=%d\n",p.String(),p.IsValid(),p.Filename,p.Offset,p.Line,p.Column)
 }
 // mutate fields
 var p token.Position
 p.Filename="z.go"; p.Line=1; p.Column=2
 fmt.Println(p.String(),p.IsValid())
 // Pos.IsValid
 fmt.Println(token.NoPos.IsValid(),token.Pos(5).IsValid())
}
