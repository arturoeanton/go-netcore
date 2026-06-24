package main
import ("fmt";"text/template";"bytes")
func main(){
 for _,s:=range []string{`a<b>&"'`,"x\x00y","plain","100% & <tag>"}{ fmt.Printf("%q\n",template.HTMLEscapeString(s)) }
 for _,s:=range []string{`a"b'c\d`,"<x>&y=z","tab\tnl\n","café"}{ fmt.Printf("%q\n",template.JSEscapeString(s)) }
 fmt.Printf("%q\n",template.URLQueryEscaper("a b","c&d"))
 fmt.Printf("%q\n",template.HTMLEscaper("x<y>",42))
 fmt.Printf("%q\n",template.JSEscaper("a<b"))
 var b bytes.Buffer; template.HTMLEscape(&b,[]byte("a<b>")); fmt.Printf("%q\n",b.String())
 var b2 bytes.Buffer; template.JSEscape(&b2,[]byte("x=y&z")); fmt.Printf("%q\n",b2.String())
}
