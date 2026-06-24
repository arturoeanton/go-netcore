package main
import ("fmt";"go/token")
func main(){
 toks:=[]token.Token{token.ILLEGAL,token.IDENT,token.INT,token.ADD,token.MUL,token.LAND,token.LOR,token.EQL,token.ASSIGN,token.LPAREN,token.BREAK,token.FUNC,token.RETURN,token.TILDE,token.SEMICOLON}
 for _,t:=range toks{
  fmt.Printf("%-10s lit=%v op=%v kw=%v prec=%d\n",t.String(),t.IsLiteral(),t.IsOperator(),t.IsKeyword(),t.Precedence())
 }
 fmt.Println(token.Token(999).String())
 // pkg funcs
 names:=[]string{"break","Foo","foo","x9","_x","9x","","range","αβ","Hello"}
 for _,n:=range names{
  fmt.Printf("%q kw=%v ident=%v exported=%v lookup=%s\n",n,token.IsKeyword(n),token.IsIdentifier(n),token.IsExported(n),token.Lookup(n).String())
 }
}
