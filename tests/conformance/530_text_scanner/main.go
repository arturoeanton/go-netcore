package main
import ("fmt";"text/scanner")
func main(){
 toks:=[]rune{scanner.EOF,scanner.Ident,scanner.Int,scanner.Float,scanner.Char,scanner.String,scanner.RawString,scanner.Comment,'+','(',';','世','\n','\t'}
 for _,t:=range toks{ fmt.Printf("%d -> %s\n",t,scanner.TokenString(t)) }
 ps:=[]scanner.Position{
  {Filename:"a.go",Offset:5,Line:2,Column:3},
  {Line:7},
  {Filename:"x"},
  {},
 }
 for _,p:=range ps{ fmt.Printf("%q valid=%v off=%d\n",p.String(),p.IsValid(),p.Offset) }
}
