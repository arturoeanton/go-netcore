package main
import ("fmt";"mime")
func main(){
 q:=mime.QEncoding; b:=mime.BEncoding
 cases:=[]struct{cs,s string}{
  {"UTF-8","Hello World"},          // no encoding needed
  {"UTF-8","Héllo"},
  {"UTF-8","¡Hola, señor!"},
  {"UTF-8","caña"},
  {"ISO-8859-1","Caf\xe9"},
  {"UTF-8","a=b?c_d e"},
  {"UTF-8","日本語のテキスト"},
  {"UTF-8","This is a fairly long subject line with some accents like é and ñ that will need to be split across multiple encoded words because it exceeds the limit"},
  {"UTF-8","tab\there"},
 }
 for _,c:=range cases{
  fmt.Printf("Q %q\n",q.Encode(c.cs,c.s))
  fmt.Printf("B %q\n",b.Encode(c.cs,c.s))
 }
}
